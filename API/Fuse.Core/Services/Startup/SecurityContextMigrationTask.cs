using Fuse.Core.Areas.Account;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services.Startup;

/// <summary>
/// One-time migration that copies users, sessions, and API keys from the legacy
/// <see cref="SecurityState"/> into the new <see cref="SecurityContext"/>.
/// Safe to re-run on every startup — already-migrated records are skipped (idempotent).
///
/// Role migration is scaffolded but intentionally incomplete: the old <see cref="Permission"/>
/// enum must first be mapped to the new string-based permission catalogs before roles can be
/// fully ported.  See the TODO comment in <see cref="MigrateRoles"/> below.
///
/// Ordering: runs after SecurityRoleSeedTask (2) and LegacyRoleMigrationTask (3) so that
/// the built-in roles already exist in the new context when users are assigned to them.
/// </summary>
public class SecurityContextMigrationTask(IFuseStore store) : IStartupTask
{
    public int Order => 4;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var legacy = snapshot.Security;
        var context = snapshot.SecurityContext;

        var migratedUsers = MigrateUsers(legacy, context);
        var migratedSessions = MigrateSessions(legacy, context);
        var migratedApiKeys = MigrateApiKeys(legacy, context);
        var migratedRoles = MigrateRoles(legacy, context);

        var anyChange =
            migratedUsers.Count != context.Users.Count ||
            migratedSessions.Count != context.Sessions.Count ||
            migratedApiKeys.Count != context.ApiKeys.Count ||
            migratedRoles.Count != context.Roles.Count;

        if (!anyChange)
            return;

        await store.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Users = migratedUsers,
                Sessions = migratedSessions,
                ApiKeys = migratedApiKeys,
                Roles = migratedRoles
            }
        }, ct);
    }

    // -------------------------------------------------------------------------
    // Users
    // -------------------------------------------------------------------------

    private static List<FuseUser> MigrateUsers(SecurityState legacy, SecurityContext context)
    {
        var existingIds = context.Users.Select(u => u.Id).ToHashSet();

        var incoming = legacy.Users
            .Where(u => !existingIds.Contains(u.Id))
            .Select(u => new FuseUser(
                Id: u.Id,
                UserName: u.UserName,
                PasswordHash: u.PasswordHash,
                PasswordSalt: u.PasswordSalt,
                IsAdmin: u.Role == SecurityRole.Admin || u.RoleIds.Contains(BuiltInRoles.AdminRoleId),
                RoleIds: u.RoleIds,
                CreatedAt: u.CreatedAt,
                UpdatedAt: u.UpdatedAt
            ));

        return context.Users.Concat(incoming).ToList();
    }

    // -------------------------------------------------------------------------
    // Sessions
    // -------------------------------------------------------------------------

    private static List<Session> MigrateSessions(SecurityState legacy, SecurityContext context)
    {
        var existingTokens = context.Sessions.Select(s => s.Token).ToHashSet();
        var now = DateTime.UtcNow;

        var incoming = legacy.Sessions
            .Where(s => !existingTokens.Contains(s.Token) && s.ExpiresAt > now)
            .Select(s => new Session(s.Token, s.UserId, s.ExpiresAt));

        return context.Sessions.Concat(incoming).ToList();
    }

    // -------------------------------------------------------------------------
    // API Keys
    // -------------------------------------------------------------------------

    /// <remarks>
    /// The legacy <see cref="ApiKey"/> model does not store a key prefix; only the
    /// hash and salt are persisted.  The new <see cref="FuseApiKey"/> model requires a
    /// prefix for O(1) lookup during verification.  Migrated keys receive an empty
    /// prefix, which means they will no longer verify — affected users will need to
    /// regenerate their API keys after migration.
    /// </remarks>
    private static List<FuseApiKey> MigrateApiKeys(SecurityState legacy, SecurityContext context)
    {
        var existingIds = context.ApiKeys.Select(k => k.Id).ToHashSet();

        var incoming = legacy.ApiKeys
            .Where(k => !existingIds.Contains(k.Id))
            .Select(k => new FuseApiKey(
                Id: k.Id,
                Name: k.Name,
                KeyPrefix: string.Empty, // Cannot be recovered — key must be regenerated
                KeyHash: k.KeyHash,
                KeySalt: k.KeySalt,
                UserId: k.UserId,
                RoleIds: k.RoleIds,
                CreatedAt: k.CreatedAt,
                UpdatedAt: k.UpdatedAt
            ));

        return context.ApiKeys.Concat(incoming).ToList();
    }

    // -------------------------------------------------------------------------
    // Roles
    // -------------------------------------------------------------------------

    /// <remarks>
    /// Role migration is a scaffold only.  The old <see cref="Permission"/> enum values
    /// must be mapped to the new string-based permission keys defined in each
    /// <see cref="AreaPermissions"/> catalog before this can be completed.
    ///
    /// TODO: Once the full Permission → string map exists, replace the empty permissions
    /// list below with a call to the mapping helper, e.g.:
    ///   Permissions: r.Permissions.Select(PermissionMap.ToKey).ToList()
    ///
    /// Until then, migrated roles carry no permissions and will need to be reconfigured
    /// manually through the role management UI.
    /// </remarks>
    private static List<FuseRole> MigrateRoles(SecurityState legacy, SecurityContext context)
    {
        var existingIds = context.Roles.Select(r => r.Id).ToHashSet();

        var incoming = legacy.Roles
            .Where(r => !existingIds.Contains(r.Id))
            .Select(r => new FuseRole(
                Id: r.Id,
                Name: r.Name,
                Description: r.Description,
                Permissions: MapPermissions(r.Permissions),
                CreatedAt: r.CreatedAt,
                UpdatedAt: r.UpdatedAt
            ));

        return context.Roles.Concat(incoming).ToList();
    }

    private static List<string> MapPermissions(IReadOnlyList<Permission> permissions)
    {
        var output = new List<string>();

        foreach (var permission in permissions)
        {
            switch (permission)
            {
                case Permission.AccountsRead:
                    output.Add(AccountPermissions.ReadKey);
                    break;
                case Permission.AccountsCreate:
                    output.Add(AccountPermissions.CreateKey);
                    break;
                case Permission.AccountsUpdate:
                    output.Add(AccountPermissions.UpdateKey);
                    break;
                case Permission.AccountsDelete:
                    output.Add(AccountPermissions.DeleteKey);
                    break;

                // TODO: add cases here as more AreaPermissions catalogs are created

                default:
                    // Swallow unmapped permissions until the full catalog is built
                    break;
            }
        }

        return output.Distinct().ToList();
    }
}
