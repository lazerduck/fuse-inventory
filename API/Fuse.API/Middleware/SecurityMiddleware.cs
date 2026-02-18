using System.Security.Claims;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.API.Middleware;

public sealed class SecurityMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISecurityService securityService, IPermissionService permissionService)
    {
        var cancellationToken = context.RequestAborted;
        var path = context.Request.Path;
        var state = await securityService.GetSecurityStateAsync(cancellationToken);
        var token = ExtractToken(context.Request);
        SecurityUser? user = null;
        if (token is not null)
        {
            user = await securityService.ValidateSessionAsync(token, refresh: true, cancellationToken);
        }

        if (user is not null)
            AttachPrincipal(context, user);

        var isSecurityEndpoint = path.StartsWithSegments("/api/security", StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWithSegments("/api/roles", StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWithSegments("/api/role", StringComparison.OrdinalIgnoreCase);
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
            // Enforce permission-based or admin-only access for specific endpoints
            if (path.StartsWithSegments("/api/audit", StringComparison.OrdinalIgnoreCase))
            {
                if (user is null)
                {
                    await WriteUnauthorizedAsync(context, cancellationToken);
                    return;
                }
                
                // Check for AuditLogsView permission
                if (!await permissionService.HasPermissionAsync(user, Permission.AuditLogsView, cancellationToken))
                {
                    if (!await permissionService.IsUserAdminAsync(user, cancellationToken)) // Fallback to admin check
                    {
                        await WriteForbiddenAsync(context, cancellationToken);
                        return;
                    }
                }
            }

            // Enforce permission-based or admin-only access for Config export endpoint (exposes secrets)
            if (path.StartsWithSegments("/api/config/export", StringComparison.OrdinalIgnoreCase))
            {
                if (user is null)
                {
                    await WriteUnauthorizedAsync(context, cancellationToken);
                    return;
                }
                
                // Check for ConfigurationExport permission
                if (!await permissionService.HasPermissionAsync(user, Permission.ConfigurationExport, cancellationToken))
                {
                    if (!await permissionService.IsUserAdminAsync(user, cancellationToken)) // Fallback to admin check
                    {
                        await WriteForbiddenAsync(context, cancellationToken);
                        return;
                    }
                }
            }

            // Enforce granular permissions for CRUD endpoints
            var crudPermission = GetRequiredCrudPermission(path, context.Request.Method, permissionService);
            var permissionHandled = false;
            if (crudPermission.HasValue)
            {
                if (user is null)
                {
                    await WriteUnauthorizedAsync(context, cancellationToken);
                    return;
                }

                if (!await permissionService.HasPermissionAsync(user, crudPermission.Value, cancellationToken))
                {
                    if (!await permissionService.IsUserAdminAsync(user, cancellationToken)) // Fallback to admin check
                    {
                        await WriteForbiddenAsync(context, cancellationToken);
                        return;
                    }
                }

                // Permission was checked and satisfied, skip legacy security-level enforcement
                permissionHandled = true;
            }

            if (!permissionHandled)
            {
                var requirement = GetRequirement(state.Settings.Level, context.Request.Method);
                if (!await AuthorizeAsync(requirement, user, context, cancellationToken))
                    return;
            }
        }

        await _next(context);
    }

    private static async Task<bool> AuthorizeAsync(AccessRequirement requirement, SecurityUser? user, HttpContext context, CancellationToken cancellationToken)
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

            // Note: This legacy access requirement check still uses direct role check
            // because it's only used during the transition period before full permission migration
            if (user.Role != SecurityRole.Admin)
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
            if (HttpMethods.IsGet(method))
                return await permissionService.HasPermissionAsync(user, Permission.UsersRead, cancellationToken) || user.Role == SecurityRole.Admin;
            if (HttpMethods.IsPost(method))
                return await permissionService.HasPermissionAsync(user, Permission.UsersCreate, cancellationToken) || user.Role == SecurityRole.Admin;
            if (HttpMethods.IsPatch(method) || HttpMethods.IsPut(method))
                return await permissionService.HasPermissionAsync(user, Permission.UsersUpdate, cancellationToken) || user.Role == SecurityRole.Admin;
            if (HttpMethods.IsDelete(method))
                return await permissionService.HasPermissionAsync(user, Permission.UsersDelete, cancellationToken) || user.Role == SecurityRole.Admin;
        }

        // Role management endpoints
        if (path.StartsWithSegments("/api/security/roles", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api/roles", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api/role", StringComparison.OrdinalIgnoreCase))
        {
            if (HttpMethods.IsGet(method))
                return await permissionService.HasPermissionAsync(user, Permission.RolesRead, cancellationToken) || user.Role == SecurityRole.Admin;
            if (HttpMethods.IsPost(method))
                return await permissionService.HasPermissionAsync(user, Permission.RolesCreate, cancellationToken) || user.Role == SecurityRole.Admin;
            if (HttpMethods.IsPatch(method) || HttpMethods.IsPut(method))
                return await permissionService.HasPermissionAsync(user, Permission.RolesUpdate, cancellationToken) || user.Role == SecurityRole.Admin;
            if (HttpMethods.IsDelete(method))
                return await permissionService.HasPermissionAsync(user, Permission.RolesDelete, cancellationToken) || user.Role == SecurityRole.Admin;
        }

        // All other security endpoints require admin role (fallback)
        return user.Role == SecurityRole.Admin;
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

    /// <summary>
    /// Determines the required permission for a CRUD endpoint based on the request path and method.
    /// Returns null if the endpoint doesn't have a granular permission mapping.
    /// </summary>
    private static Permission? GetRequiredCrudPermission(PathString path, string method, IPermissionService permissionService)
    {
        // Extract controller name and determine action
        var (controllerName, actionName) = ExtractControllerAndAction(path, method);
        
        if (string.IsNullOrEmpty(controllerName) || string.IsNullOrEmpty(actionName))
            return null;

        return permissionService.GetRequiredPermission(
            controllerName,
            actionName,
            method.ToUpperInvariant());
    }

    /// <summary>
    /// Extracts the controller name and determines the action name from the request path and HTTP method.
    /// Returns (controllerName, actionName) or (null, null) if extraction fails.
    /// </summary>
    private static (string? controllerName, string? actionName) ExtractControllerAndAction(PathString path, string method)
    {
        var pathStr = path.Value ?? "";
        
        // Remove /api/ prefix
        if (!pathStr.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            return (null, null);

        pathStr = pathStr[5..]; // Remove "/api/"

        // Split path into segments
        var segments = pathStr.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return (null, null);

        var controllerSegment = segments[0];

        // Skip security endpoints - handled separately
        if (controllerSegment.Equals("security", StringComparison.OrdinalIgnoreCase) ||
            controllerSegment.Equals("roles", StringComparison.OrdinalIgnoreCase))
            return (null, null);

        // Map controller segment to proper PascalCase
        var controllerName = NormalizeControllerName(controllerSegment);
        if (string.IsNullOrEmpty(controllerName))
            return (null, null);

        // Determine action based on HTTP method and route structure
        string actionName = method.ToUpperInvariant() switch
        {
            "GET" => segments.Length > 1 ? "Get" : "GetAll",
            "POST" => "Create",
            "PUT" => "Update",
            "PATCH" => "Update",
            "DELETE" => "Delete",
            _ => ""
        };

        // Handle special actions in POST requests (e.g., Risk.Approve.POST, Account.ApplyGrants.POST)
        if (HttpMethods.IsPost(method) && segments.Length > 1)
        {
            var lastSegment = segments[^1];
            // Check if last segment is a special action (not a GUID or ID)
            if (!IsGuidOrId(lastSegment))
            {
                // Capitalize the action name
                actionName = char.ToUpperInvariant(lastSegment[0]) + lastSegment[1..];
            }
        }

        return (controllerName, actionName);
    }

    private static string? NormalizeControllerName(string controllerSegment)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application"] = "Application",
            ["account"] = "Account",
            ["identity"] = "Identity",
            ["datastore"] = "DataStore",
            ["platform"] = "Platform",
            ["environment"] = "Environment",
            ["externalresource"] = "ExternalResource",
            ["position"] = "Position",
            ["responsibilitytype"] = "ResponsibilityType",
            ["responsibilityassignment"] = "ResponsibilityAssignment",
            ["risk"] = "Risk",
            ["secretprovider"] = "SecretProvider",
            ["sqlintegration"] = "SqlIntegration",
            ["kumaintegration"] = "KumaIntegration",
            ["config"] = "Config",
            ["audit"] = "Audit",
            ["tag"] = "Tag",
            ["role"] = "Role"
        };

        return map.TryGetValue(controllerSegment, out var controllerName)
            ? controllerName
            : null;
    }

    /// <summary>
    /// Determines if a path segment is a GUID or numeric ID.
    /// </summary>
    private static bool IsGuidOrId(string segment)
    {
        // Check if it's a GUID
        if (Guid.TryParse(segment, out _))
            return true;

        // Check if it's a number
        if (int.TryParse(segment, out _))
            return true;

        return false;
    }
}
