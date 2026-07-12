using System.Security.Claims;
using Fuse.API.Middleware;
using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.API.Mcp;

public sealed class McpToolAuthorization(
    IHttpContextAccessor contextAccessor,
    IFuseStore store,
    IFuseRoleService roleService,
    IEnumerable<AreaPermissions> permissionCatalogs)
{
    public async Task RequireAsync(string permissionKey, CancellationToken ct)
    {
        var user = contextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Authentication is required.");

        if (string.Equals(user.FindFirst(AuthenticationMiddleware.IsAdminClaimType)?.Value,
                bool.TrueString, StringComparison.OrdinalIgnoreCase))
            return;

        var descriptor = permissionCatalogs
            .Select(c => c.TryGetPermissionDescriptor(permissionKey))
            .FirstOrDefault(d => d is not null);
        var posture = await store.GetAsync(s => s.SecurityContext.Posture, ct);

        if (posture == SecurityPosture.Unrestricted && descriptor?.IgnorePosture != true)
            return;
        if (posture == SecurityPosture.RestrictedEditing
            && descriptor is { IgnorePosture: not true, IsAllowedInRestrictedEditing: true })
            return;

        var roleIds = user.FindAll(AuthenticationMiddleware.RoleIdClaimType)
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        if (roleIds.Count == 0)
            throw new UnauthorizedAccessException($"Permission '{permissionKey}' is required.");

        var roles = await roleService.GetRolesByIds(roleIds);
        if (!roles.IsSuccess || !roles.Value!.SelectMany(r => r.Permissions)
                .Contains(permissionKey, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"Permission '{permissionKey}' is required.");
    }
}
