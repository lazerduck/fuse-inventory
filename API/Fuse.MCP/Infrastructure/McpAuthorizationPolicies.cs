using Fuse.Core.Areas.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Fuse.MCP;

public sealed record McpPermissionRequirement(IReadOnlyList<string> AnyOf) : IAuthorizationRequirement;

public sealed class McpAuthorizationPolicies(IEnumerable<AreaPermissions> catalogs) : IConfigureOptions<AuthorizationOptions>
{
    public const string InventoryRead = "mcp:inventory:read";

    public void Configure(AuthorizationOptions options)
    {
        foreach (var key in catalogs.SelectMany(c => c.GetPermissions()).Distinct(StringComparer.OrdinalIgnoreCase))
            options.AddPolicy(key, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new McpPermissionRequirement([key])));

        // Discovery has no entityType argument. Show generic read tools if any supported
        // entity is readable; their handlers still authorize the requested entity type.
        options.AddPolicy(InventoryRead, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new McpPermissionRequirement(Enum.GetValues<InventoryEntityType>()
                .Select(InventoryReadTools.ReadPermission).Distinct().ToArray())));
    }
}

public sealed class McpPermissionHandler(McpToolAuthorization authorization)
    : AuthorizationHandler<McpPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, McpPermissionRequirement requirement)
    {
        foreach (var permission in requirement.AnyOf)
        {
            if (!await authorization.HasPermissionAsync(context.User, permission, default)) continue;
            context.Succeed(requirement);
            return;
        }
    }
}
