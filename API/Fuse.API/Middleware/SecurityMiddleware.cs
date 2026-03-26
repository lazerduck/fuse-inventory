using System.Security.Claims;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;

namespace Fuse.API.Middleware;

public sealed class SecurityMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISecurityService securityService, IPermissionService permissionService, IVersionHistoryService versionHistoryService)
    {
        var cancellationToken = context.RequestAborted;
        var path = context.Request.Path;
        var state = await securityService.GetSecurityStateAsync(cancellationToken);

        // Prefer x-api-key over Bearer token when both are present
        var apiKeyHeader = ExtractApiKey(context.Request);
        SecurityUser? user = null;

        if (apiKeyHeader is not null)
        {
            var (apiKeyUser, apiKey) = await securityService.ValidateApiKeyAsync(apiKeyHeader, cancellationToken);
            if (apiKeyUser is not null && apiKey is not null)
            {
                // For API key auth we create a synthetic user that has the key's role IDs
                user = apiKeyUser with { RoleIds = apiKey.RoleIds };
            }
        }

        if (user is null)
        {
            var token = ExtractToken(context.Request);
            if (token is not null)
            {
                user = await securityService.ValidateSessionAsync(token, refresh: true, cancellationToken);
            }
        }

        if (user is not null)
            AttachPrincipal(context, user);

        // Set user context for change tracking (flows via AsyncLocal)
        SnapshotChangeTracker.SetUserContext(
            user?.UserName ?? "anonymous",
            user?.Id
        );

        var isSecurityEndpoint = path.StartsWithSegments("/api/security", StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWithSegments("/api/roles", StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWithSegments("/api/role", StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWithSegments("/api/apikey", StringComparison.OrdinalIgnoreCase);
        var requiresSetup = state.RequiresSetup;

        if (requiresSetup && !IsSetupAllowed(path, context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Initial administrator setup is required before accessing the API.",
                requiresSetup = true
            }, cancellationToken);
            return;
        }

        if (isSecurityEndpoint)
        {
            // Security endpoints require admin access or specific permissions
            if (!await IsSecurityEndpointAllowedAsync(path, context.Request.Method, user, requiresSetup, permissionService, cancellationToken))
            {
                if (user is null)
                {
                    await WriteUnauthorizedAsync(context, cancellationToken);
                    return;
                }
                await WriteForbiddenAsync(context, cancellationToken);
                return;
            }
        }
        else
        {
            // First check site-wide security level - it should override user-specific permissions
            // when the site-wide setting is more permissive
            var requirement = GetRequirement(state.Settings.Level, context.Request.Method);
            
            // If site-wide level allows public access, allow without authentication
            if (requirement == AccessRequirement.Public)
            {
                await _next(context);
                return;
            }
            
            // Undo has dynamic, entity-specific permission requirements determined at runtime
            if (path.StartsWithSegments("/api/undo", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(context.Request.Method))
            {
                if (user is null)
                {
                    await WriteUnauthorizedAsync(context, cancellationToken);
                    return;
                }

                if (!await HasUndoPermissionAsync(path, user, permissionService, versionHistoryService, cancellationToken)
                    && !await permissionService.IsUserAdminAsync(user, cancellationToken))
                {
                    await WriteForbiddenAsync(context, cancellationToken);
                    return;
                }
            }
            else
            {
                // Read the required permission from the endpoint's [RequirePermission] attribute.
                // Routing must have run before this middleware (UseRouting() in Program.cs).
                var endpoint = context.GetEndpoint();
                var requiredPermission = endpoint?.Metadata.GetMetadata<RequirePermissionAttribute>()?.Permission;

                if (requiredPermission.HasValue)
                {
                    if (user is null)
                    {
                        await WriteUnauthorizedAsync(context, cancellationToken);
                        return;
                    }

                    if (!await permissionService.HasPermissionAsync(user, requiredPermission.Value, cancellationToken)
                        && !await permissionService.IsUserAdminAsync(user, cancellationToken))
                    {
                        await WriteForbiddenAsync(context, cancellationToken);
                        return;
                    }
                }
                else
                {
                    // No specific permission declared — fall back to site-wide security level
                    if (!await AuthorizeAsync(requirement, user, context, permissionService, cancellationToken))
                        return;
                }
            }
        }

        await _next(context);
    }

    private static async Task<bool> HasUndoPermissionAsync(
        PathString path,
        SecurityUser user,
        IPermissionService permissionService,
        IVersionHistoryService versionHistoryService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUndoVersionId(path, out var versionId))
            return true;

        var version = await versionHistoryService.GetVersionByIdAsync(versionId, cancellationToken);
        if (version is null)
        {
            // Let controller return not found for unknown versions.
            return true;
        }

        var requiredPermission = UndoPermissionMapper.ToPermission(version.EntityType);
        return await permissionService.HasPermissionAsync(user, requiredPermission, cancellationToken);
    }

    private static bool TryGetUndoVersionId(PathString path, out Guid versionId)
    {
        versionId = Guid.Empty;
        var pathStr = path.Value;
        if (string.IsNullOrWhiteSpace(pathStr))
            return false;

        var segments = pathStr.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3)
            return false;

        if (!segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) ||
            !segments[1].Equals("undo", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParse(segments[2], out versionId);
    }

    private static async Task<bool> AuthorizeAsync(AccessRequirement requirement, SecurityUser? user, HttpContext context, IPermissionService permissionService, CancellationToken cancellationToken)
    {
        if (requirement == AccessRequirement.Public)
            return true;

        if (requirement == AccessRequirement.Read)
        {
            if (user is null)
            {
                await WriteUnauthorizedAsync(context, cancellationToken);
                return false;
            }
            return true;
        }

        if (requirement == AccessRequirement.Admin)
        {
            if (user is null)
            {
                await WriteUnauthorizedAsync(context, cancellationToken);
                return false;
            }

            if (!await permissionService.IsUserAdminAsync(user, cancellationToken))
            {
                await WriteForbiddenAsync(context, cancellationToken);
                return false;
            }

            return true;
        }

        return true;
    }

    private static Task WriteUnauthorizedAsync(HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new { error = "Authentication required." }, cancellationToken);
    }

    private static Task WriteForbiddenAsync(HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new { error = "Administrator privileges are required." }, cancellationToken);
    }

    private static void AttachPrincipal(HttpContext context, SecurityUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, "FuseSecurity");
        context.User = new ClaimsPrincipal(identity);
    }

    private static string? ExtractToken(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var values))
            return null;

        var header = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header))
            return null;

        const string prefix = "Bearer ";
        if (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return header[prefix.Length..].Trim();

        return null;
    }

    private static string? ExtractApiKey(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("x-api-key", out var values))
            return null;

        var key = values.FirstOrDefault();
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    private static bool IsSetupAllowed(PathString path, string method)
    {
        if (path.StartsWithSegments("/api/security/state", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.StartsWithSegments("/api/security/accounts", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(method))
            return true;
        if (path.StartsWithSegments("/api/security/login", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(method))
            return true;
        return false;
    }

    private static async Task<bool> IsSecurityEndpointAllowedAsync(PathString path, string method, SecurityUser? user, bool requiresSetup, IPermissionService permissionService, CancellationToken cancellationToken)
    {
        // During setup, allow specific endpoints without authentication
        if (requiresSetup && IsSetupAllowed(path, method))
            return true;

        // Allow state endpoint for authenticated users (needed by UI to check security state)
        if (path.StartsWithSegments("/api/security/state", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsGet(method))
            return true;

        // Allow login and logout for all
        if (path.StartsWithSegments("/api/security/login", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(method))
            return true;
        if (path.StartsWithSegments("/api/security/logout", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(method))
            return true;

        if (user is null)
            return false;

        // Check specific permissions for security endpoints
        if (path.StartsWithSegments("/api/security/accounts", StringComparison.OrdinalIgnoreCase))
        {
            if (HttpMethods.IsPost(method) && TryGetPasswordResetTargetUserId(path, out var targetUserId))
            {
                if (user.Id == targetUserId)
                    return true;

                return await permissionService.IsUserAdminAsync(user, cancellationToken);
            }

            if (HttpMethods.IsGet(method))
                return await permissionService.HasPermissionAsync(user, Permission.UsersRead, cancellationToken) || await permissionService.IsUserAdminAsync(user, cancellationToken);
            if (HttpMethods.IsPost(method))
                return await permissionService.HasPermissionAsync(user, Permission.UsersCreate, cancellationToken) || await permissionService.IsUserAdminAsync(user, cancellationToken);
            if (HttpMethods.IsPatch(method) || HttpMethods.IsPut(method))
                return await permissionService.HasPermissionAsync(user, Permission.UsersUpdate, cancellationToken) || await permissionService.IsUserAdminAsync(user, cancellationToken);
            if (HttpMethods.IsDelete(method))
                return await permissionService.HasPermissionAsync(user, Permission.UsersDelete, cancellationToken) || await permissionService.IsUserAdminAsync(user, cancellationToken);
        }

        // Role management endpoints
        if (path.StartsWithSegments("/api/security/roles", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api/roles", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api/role", StringComparison.OrdinalIgnoreCase))
        {
            if (HttpMethods.IsGet(method) && TryGetRoleIdFromPath(path, out var roleId) && user.RoleIds.Contains(roleId))
                return true;

            if (HttpMethods.IsGet(method))
                return await permissionService.HasPermissionAsync(user, Permission.RolesRead, cancellationToken) || await permissionService.IsUserAdminAsync(user, cancellationToken);
            if (HttpMethods.IsPost(method))
                return await permissionService.HasPermissionAsync(user, Permission.RolesCreate, cancellationToken) || await permissionService.IsUserAdminAsync(user, cancellationToken);
            if (HttpMethods.IsPatch(method) || HttpMethods.IsPut(method))
                return await permissionService.HasPermissionAsync(user, Permission.RolesUpdate, cancellationToken) || await permissionService.IsUserAdminAsync(user, cancellationToken);
            if (HttpMethods.IsDelete(method))
                return await permissionService.HasPermissionAsync(user, Permission.RolesDelete, cancellationToken) || await permissionService.IsUserAdminAsync(user, cancellationToken);
        }

        // API key management endpoints - users can manage their own keys
        if (path.StartsWithSegments("/api/apikey", StringComparison.OrdinalIgnoreCase))
        {
            if (user is null)
                return false;

            // Any authenticated user can create, list, regenerate, delete their own API keys
            return true;
        }

        // All other security endpoints require admin role (fallback)
        return await permissionService.IsUserAdminAsync(user, cancellationToken);
    }

    private static bool TryGetPasswordResetTargetUserId(PathString path, out Guid targetUserId)
    {
        targetUserId = Guid.Empty;

        var pathStr = path.Value;
        if (string.IsNullOrWhiteSpace(pathStr))
            return false;

        var segments = pathStr.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 5)
            return false;

        if (!segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) ||
            !segments[1].Equals("security", StringComparison.OrdinalIgnoreCase) ||
            !segments[2].Equals("accounts", StringComparison.OrdinalIgnoreCase) ||
            !segments[4].Equals("reset-password", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParse(segments[3], out targetUserId);
    }

    private static bool TryGetRoleIdFromPath(PathString path, out Guid roleId)
    {
        roleId = Guid.Empty;

        var pathStr = path.Value;
        if (string.IsNullOrWhiteSpace(pathStr))
            return false;

        var segments = pathStr.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3)
            return false;

        if (!segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
            return false;

        var roleSegment = segments[1];
        if (!roleSegment.Equals("role", StringComparison.OrdinalIgnoreCase) &&
            !roleSegment.Equals("roles", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParse(segments[2], out roleId);
    }

    private static AccessRequirement GetRequirement(SecurityLevel level, string method)
    {
        if (level == SecurityLevel.None)
            return AccessRequirement.Public;

        var isRead = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

        if (level == SecurityLevel.RestrictedEditing)
            return isRead ? AccessRequirement.Public : AccessRequirement.Admin;

        return isRead ? AccessRequirement.Read : AccessRequirement.Admin;
    }

    private enum AccessRequirement
    {
        Public,
        Read,
        Admin
    }
}
