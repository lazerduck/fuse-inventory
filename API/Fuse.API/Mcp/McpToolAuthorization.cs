using System.Security.Claims;
using Fuse.API.Middleware;
using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Models;
using ModelContextProtocol;

namespace Fuse.API.Mcp;

public sealed class McpToolAuthorization(
    IHttpContextAccessor contextAccessor,
    IFuseRoleService roleService,
    IEnumerable<AreaPermissions> permissionCatalogs)
{
    public async Task RequireAsync(string permissionKey, CancellationToken ct)
    {
        var user = contextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            throw new McpException("Authentication is required.");

        if (string.Equals(user.FindFirst(AuthenticationMiddleware.IsAdminClaimType)?.Value,
                bool.TrueString, StringComparison.OrdinalIgnoreCase))
            return;

        var descriptor = permissionCatalogs
            .Select(c => c.TryGetPermissionDescriptor(permissionKey))
            .FirstOrDefault(d => d is not null);
        if (descriptor is null)
            throw new McpException($"The MCP tool is configured with unknown permission '{permissionKey}'.");

        var roleIds = user.FindAll(AuthenticationMiddleware.RoleIdClaimType)
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        if (roleIds.Count == 0)
            throw new McpException($"The API key requires the '{permissionKey}' permission.");

        var roles = await roleService.GetRolesByIds(roleIds);
        if (!roles.IsSuccess || !roles.Value!.SelectMany(r => r.Permissions)
                .Contains(permissionKey, StringComparer.OrdinalIgnoreCase))
            throw new McpException($"The API key requires the '{permissionKey}' permission.");
    }
}
