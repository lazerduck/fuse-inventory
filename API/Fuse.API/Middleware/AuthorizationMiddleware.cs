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

    public async Task InvokeAsync(HttpContext context, IFuseStore fuseStore, IFuseRoleService roleService)
    {
        var cancellationToken = context.RequestAborted;
        var user = context.User;

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

        switch (posture)
        {
            case SecurityPosture.Unrestricted:
                await _next(context);
                return;

            case SecurityPosture.RestrictedEditing:
                // TODO: implement restricted-editing rules
                await _next(context);
                return;

            case SecurityPosture.FullyRestricted:
                var endpoint = context.GetEndpoint();
                var requiredKey = endpoint?.Metadata.GetMetadata<RequirePermissionKeyAttribute>()?.PermissionKey;

                if (requiredKey is null)
                {
                    await _next(context);
                    return;
                }

                if (!user.Identity?.IsAuthenticated ?? true)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                var roleIdClaims = user.FindAll(AuthenticationMiddleware.RoleIdClaimType);
                var roleIds = roleIdClaims
                    .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();

                if (roleIds.Count > 0)
                {
                    var rolesResult = await roleService.GetRolesByIds(roleIds);
                    if (rolesResult.IsSuccess)
                    {
                        var hasPermission = rolesResult.Value!
                            .Any(r => r.Permissions.Contains(requiredKey, StringComparer.OrdinalIgnoreCase));

                        if (hasPermission)
                        {
                            await _next(context);
                            return;
                        }
                    }
                }

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
        }
    }
}
