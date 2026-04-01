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
        var requiredKey = context.GetEndpoint()?.Metadata
            .GetMetadata<RequirePermissionKeyAttribute>()?.PermissionKey;

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
        var descriptor = ResolveDescriptor(requiredKey, permissionCatalogs);

        switch (posture)
        {
            case SecurityPosture.Unrestricted:
                if (requiredKey is null)
                {
                    await _next(context);
                    return;
                }

                if (descriptor?.IgnorePosture != true)
                {
                    await _next(context);
                    return;
                }

                if (await UserHasPermissionAsync(user, requiredKey, roleService, cancellationToken))
                {
                    await _next(context);
                    return;
                }

                context.Response.StatusCode = user.Identity?.IsAuthenticated == true
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized;
                return;

            case SecurityPosture.RestrictedEditing:
                if (requiredKey is null)
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

                if (await UserHasPermissionAsync(user, requiredKey, roleService, cancellationToken))
                {
                    await _next(context);
                    return;
                }

                context.Response.StatusCode = user.Identity?.IsAuthenticated == true
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized;
                return;

            case SecurityPosture.FullyRestricted:
                if (requiredKey is null)
                {
                    await _next(context);
                    return;
                }

                if (await UserHasPermissionAsync(user, requiredKey, roleService, cancellationToken))
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

    private static async Task<bool> UserHasPermissionAsync(
        ClaimsPrincipal user,
        string requiredKey,
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

        return rolesResult.Value!
            .Any(r => r.Permissions.Contains(requiredKey, StringComparer.OrdinalIgnoreCase));
    }
}
