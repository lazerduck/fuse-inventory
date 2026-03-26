using Fuse.Core.Interfaces;

namespace Fuse.Core.Services.Startup;

/// <summary>
/// Ensures the built-in Admin and Reader roles exist in the store.
/// Safe to re-run on every startup (idempotent).
/// </summary>
public class SecurityRoleSeedTask : IStartupTask
{
    private readonly IFuseStore _store;
    private readonly IPermissionService _permissionService;

    public SecurityRoleSeedTask(IFuseStore store, IPermissionService permissionService)
    {
        _store = store;
        _permissionService = permissionService;
    }

    public int Order => 2;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var defaultRoles = await _permissionService.EnsureDefaultRolesAsync(ct);

        var state = await _store.GetAsync(ct);
        var existingRoleIds = state.Security.Roles.Select(r => r.Id).ToHashSet();
        var missingRoles = defaultRoles.Where(r => !existingRoleIds.Contains(r.Id)).ToList();

        if (missingRoles.Any())
        {
            await _store.UpdateAsync(s => s with
            {
                Security = s.Security with
                {
                    Roles = s.Security.Roles.Concat(missingRoles).ToList()
                }
            }, ct);
        }
    }
}
