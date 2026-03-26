using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services.Startup;

/// <summary>
/// One-time migration: appends the correct built-in role ID to any user that still only
/// carries the legacy <see cref="SecurityRole"/> enum value without an explicit role ID.
/// Safe to re-run on every startup (idempotent — skipped when nothing needs migrating).
/// </summary>
public class LegacyRoleMigrationTask : IStartupTask
{
    private readonly IFuseStore _store;

    public LegacyRoleMigrationTask(IFuseStore store)
    {
        _store = store;
    }

    public int Order => 3;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var state = await _store.GetAsync(ct);

        var needsMigration = state.Security.Users.Any(u =>
            (u.Role == SecurityRole.Admin && !u.RoleIds.Contains(BuiltInRoles.AdminRoleId)) ||
            (u.Role == SecurityRole.Reader
                && !u.RoleIds.Contains(BuiltInRoles.ReaderRoleId)
                && !u.RoleIds.Contains(BuiltInRoles.AdminRoleId)));

        if (!needsMigration)
            return;

        await _store.UpdateAsync(s => s with
        {
            Security = s.Security with
            {
                Users = s.Security.Users.Select(u =>
                {
                    if (u.Role == SecurityRole.Admin && !u.RoleIds.Contains(BuiltInRoles.AdminRoleId))
                        return u with { RoleIds = u.RoleIds.Append(BuiltInRoles.AdminRoleId).ToList() };

                    if (u.Role == SecurityRole.Reader
                        && !u.RoleIds.Contains(BuiltInRoles.ReaderRoleId)
                        && !u.RoleIds.Contains(BuiltInRoles.AdminRoleId))
                        return u with { RoleIds = u.RoleIds.Append(BuiltInRoles.ReaderRoleId).ToList() };

                    return u;
                }).ToList()
            }
        }, ct);
    }
}
