using System.Security.Claims;
using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.API.Middleware;

public sealed class AuthorizationMiddleware
{
    private readonly RequestDelegate _next;

    public AuthorizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IFuseStore fuseStore,
        IFuseRoleService roleService,
        IEnumerable<AreaPermissions> permissionCatalogs)
    {
        var cancellationToken = context.RequestAborted;
        var user = context.User;
        var endpoint = context.GetEndpoint();
        var endpointMetadata = endpoint?.Metadata;
        var permissionAttribute = endpointMetadata?.GetMetadata<RequirePermissionKeyAttribute>();
        var requiredKey = permissionAttribute?.PermissionKey;
        var requiredKeys = permissionAttribute?.PermissionKeys;
        var allowDuringSetup = endpointMetadata?.GetMetadata<AllowDuringSetupAttribute>() is not null;

        // Admins bypass all further checks
        if (string.Equals(
            user.FindFirst(AuthenticationMiddleware.IsAdminClaimType)?.Value,
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var snapshot = await fuseStore.GetAsync(cancellationToken);
        var posture = snapshot.SecurityContext.Posture;
        // When the attribute specifies multiple keys, resolve the descriptor from the primary key.
        // The RestrictedEditing short-circuit uses the most-permissive descriptor (any key allowed).
        var descriptor = ResolveDescriptor(requiredKey, permissionCatalogs);

        // During initial setup (no admin users yet), allow unauthenticated access to create
        // the first admin account so the application can be bootstrapped.
        var requiresSetup = !snapshot.SecurityContext.Users.Any(u => u.IsAdmin);
        if (requiresSetup)
        {
            if (!allowDuringSetup)
            {
                context.Response.StatusCode = user.Identity?.IsAuthenticated == true
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized;
                return;
            }

            await _next(context);
            return;
        }

        switch (posture)
        {
            case SecurityPosture.Unrestricted:
                if (requiredKeys is null)
                {
                    await _next(context);
                    return;
                }

                if (descriptor?.IgnorePosture != true)
                {
                    await _next(context);
                    return;
                }

                if (await UserHasAnyPermissionAsync(user, requiredKeys, roleService, cancellationToken))
                {
                    await _next(context);
                    return;
                }

                context.Response.StatusCode = user.Identity?.IsAuthenticated == true
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized;
                return;

            case SecurityPosture.RestrictedEditing:
                if (requiredKeys is null)
                {
                    await _next(context);
                    return;
                }

                if (descriptor is not null
                    && descriptor.IgnorePosture != true
                    && descriptor.IsAllowedInRestrictedEditing)
                {
                    await _next(context);
                    return;
                }

                if (await UserHasAnyPermissionAsync(user, requiredKeys, roleService, cancellationToken))
                {
                    await _next(context);
                    return;
                }

                context.Response.StatusCode = user.Identity?.IsAuthenticated == true
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized;
                return;

            case SecurityPosture.FullyRestricted:
                if (requiredKeys is null)
                {
                    await _next(context);
                    return;
                }

                if (await UserHasAnyPermissionAsync(user, requiredKeys, roleService, cancellationToken))
                {
                    await _next(context);
                    return;
                }

                context.Response.StatusCode = user.Identity?.IsAuthenticated == true
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized;
                return;
        }
    }

    private static PermissionDescriptor? ResolveDescriptor(
        string? permissionKey,
        IEnumerable<AreaPermissions> permissionCatalogs)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            return null;

        foreach (var catalog in permissionCatalogs)
        {
            var descriptor = catalog.TryGetPermissionDescriptor(permissionKey);
            if (descriptor is not null)
                return descriptor;
        }

        return null;
    }

    private static async Task<bool> UserHasAnyPermissionAsync(
        ClaimsPrincipal user,
        IReadOnlyList<string> requiredKeys,
        IFuseRoleService roleService,
        CancellationToken cancellationToken)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        var roleIdClaims = user.FindAll(AuthenticationMiddleware.RoleIdClaimType);
        var roleIds = roleIdClaims
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (roleIds.Count == 0)
            return false;

        var rolesResult = await roleService.GetRolesByIds(roleIds);
        if (!rolesResult.IsSuccess)
            return false;

        var allPermissions = rolesResult.Value!.SelectMany(r => r.Permissions).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requiredKeys.Any(key => allPermissions.Contains(key));
    }
}
