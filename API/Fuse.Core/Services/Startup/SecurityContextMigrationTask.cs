using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Activity;
using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.Audit;
using Fuse.Core.Areas.Config;
using Fuse.Core.Areas.DataStore;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.ExternalResource;
using Fuse.Core.Areas.Identity;
using Fuse.Core.Areas.KumaIntegration;
using Fuse.Core.Areas.MessageBroker;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.Position;
using Fuse.Core.Areas.Responsibility;
using Fuse.Core.Areas.Risk;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Areas.SqlIntegration;
using Fuse.Core.Areas.Undo;
using Fuse.Core.Areas.Security.Permissions;

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
                case Permission.ApplicationsRead:
                    output.Add(ApplicationPermissions.ReadKey);
                    output.Add(ApplicationPermissions.ReadInstanceAPIKey);
                    break;
                case Permission.ApplicationsCreate:
                    output.Add(ApplicationPermissions.CreateKey);
                    output.Add(ApplicationPermissions.CreateInstanceKey);
                    break;
                case Permission.ApplicationsUpdate:
                    output.Add(ApplicationPermissions.UpdateKey);
                    output.Add(ApplicationPermissions.UpdateInstanceKey);
                    break;
                case Permission.ApplicationsDelete:
                    output.Add(ApplicationPermissions.DeleteKey);
                    output.Add(ApplicationPermissions.DeleteInstanceKey);
                    break;

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

                case Permission.ConfigurationExport:
                    output.Add(ConfigPermissions.ExportKey);
                    break;
                case Permission.ConfigurationImport:
                    output.Add(ConfigPermissions.ImportKey);
                    break;

                case Permission.DataStoresRead:
                    output.Add(DataStorePermissions.ReadKey);
                    break;
                case Permission.DataStoresCreate:
                    output.Add(DataStorePermissions.CreateKey);
                    break;
                case Permission.DataStoresUpdate:
                    output.Add(DataStorePermissions.UpdateKey);
                    break;
                case Permission.DataStoresDelete:
                    output.Add(DataStorePermissions.DeleteKey);
                    break;

                case Permission.EnvironmentsRead:
                    output.Add(EnvironmentPermissions.ReadKey);
                    break;
                case Permission.EnvironmentsCreate:
                    output.Add(EnvironmentPermissions.CreateKey);
                    break;
                case Permission.EnvironmentsUpdate:
                    output.Add(EnvironmentPermissions.UpdateKey);
                    output.Add(EnvironmentPermissions.ApplyAutomationKey);
                    break;
                case Permission.EnvironmentsDelete:
                    output.Add(EnvironmentPermissions.DeleteKey);
                    break;

                case Permission.ExternalResourcesRead:
                    output.Add(ExternalResourcePermissions.ReadKey);
                    break;
                case Permission.ExternalResourcesCreate:
                    output.Add(ExternalResourcePermissions.CreateKey);
                    break;
                case Permission.ExternalResourcesUpdate:
                    output.Add(ExternalResourcePermissions.UpdateKey);
                    break;
                case Permission.ExternalResourcesDelete:
                    output.Add(ExternalResourcePermissions.DeleteKey);
                    break;

                case Permission.IdentitiesRead:
                    output.Add(IdentityPermissions.ReadKey);
                    break;
                case Permission.IdentitiesCreate:
                    output.Add(IdentityPermissions.CreateKey);
                    break;
                case Permission.IdentitiesUpdate:
                    output.Add(IdentityPermissions.UpdateKey);
                    break;
                case Permission.IdentitiesDelete:
                    output.Add(IdentityPermissions.DeleteKey);
                    break;

                case Permission.KumaIntegrationsCreate:
                    output.Add(KumaIntegrationPermissions.ReadKey);
                    output.Add(KumaIntegrationPermissions.CreateKey);
                    output.Add(KumaIntegrationPermissions.UpdateKey);
                    break;
                case Permission.KumaIntegrationsDelete:
                    output.Add(KumaIntegrationPermissions.ReadKey);
                    output.Add(KumaIntegrationPermissions.DeleteKey);
                    break;

                case Permission.MessageBrokersRead:
                    output.Add(MessageBrokerPermissions.ReadKey);
                    break;
                case Permission.MessageBrokersCreate:
                    output.Add(MessageBrokerPermissions.CreateKey);
                    break;
                case Permission.MessageBrokersUpdate:
                    output.Add(MessageBrokerPermissions.UpdateKey);
                    break;
                case Permission.MessageBrokersDelete:
                    output.Add(MessageBrokerPermissions.DeleteKey);
                    break;

                case Permission.PlatformsRead:
                    output.Add(PlatformPermissions.ReadKey);
                    break;
                case Permission.PlatformsCreate:
                    output.Add(PlatformPermissions.CreateKey);
                    break;
                case Permission.PlatformsUpdate:
                    output.Add(PlatformPermissions.UpdateKey);
                    break;
                case Permission.PlatformsDelete:
                    output.Add(PlatformPermissions.DeleteKey);
                    break;

                case Permission.PositionsRead:
                    output.Add(PositionPermissions.ReadKey);
                    break;
                case Permission.PositionsCreate:
                    output.Add(PositionPermissions.CreateKey);
                    break;
                case Permission.PositionsUpdate:
                    output.Add(PositionPermissions.UpdateKey);
                    break;
                case Permission.PositionsDelete:
                    output.Add(PositionPermissions.DeleteKey);
                    break;

                case Permission.ResponsibilitiesRead:
                    output.Add(ResponsibilityPermissions.ReadKey);
                    break;
                case Permission.ResponsibilitiesCreate:
                    output.Add(ResponsibilityPermissions.CreateKey);
                    break;
                case Permission.ResponsibilitiesUpdate:
                    output.Add(ResponsibilityPermissions.UpdateKey);
                    break;
                case Permission.ResponsibilitiesDelete:
                    output.Add(ResponsibilityPermissions.DeleteKey);
                    break;

                case Permission.RisksRead:
                    output.Add(RiskPermissions.ReadKey);
                    break;
                case Permission.RisksCreate:
                    output.Add(RiskPermissions.CreateKey);
                    break;
                case Permission.RisksUpdate:
                    output.Add(RiskPermissions.UpdateKey);
                    break;
                case Permission.RisksDelete:
                    output.Add(RiskPermissions.DeleteKey);
                    break;
                case Permission.RisksApprove:
                    output.Add(RiskPermissions.ApproveKey);
                    break;

                case Permission.AzureKeyVaultSecretsView:
                    output.Add(SecretProviderPermissions.ReadKey);
                    output.Add(SecretProviderPermissions.RevealSecretKey);
                    break;
                case Permission.AzureKeyVaultConnectionsCreate:
                    output.Add(SecretProviderPermissions.CreateKey);
                    output.Add(SecretProviderPermissions.UpdateKey);
                    output.Add(SecretProviderPermissions.CreateSecretKey);
                    output.Add(SecretProviderPermissions.RotateSecretKey);
                    break;
                case Permission.AzureKeyVaultConnectionsDelete:
                    output.Add(SecretProviderPermissions.DeleteKey);
                    break;

                case Permission.SqlConnectionsCreate:
                    output.Add(SqlIntegrationPermissions.ReadKey);
                    output.Add(SqlIntegrationPermissions.CreateKey);
                    output.Add(SqlIntegrationPermissions.UpdateKey);
                    break;
                case Permission.SqlConnectionsDelete:
                    output.Add(SqlIntegrationPermissions.ReadKey);
                    output.Add(SqlIntegrationPermissions.DeleteKey);
                    break;
                case Permission.SqlGrantsApply:
                    output.Add(SqlIntegrationPermissions.ReadKey);
                    output.Add(SqlIntegrationPermissions.ApplyGrantsKey);
                    break;

                case Permission.AuditLogsView:
                    output.Add(AuditPermissions.ViewKey);
                    break;

                case Permission.ActivityRead:
                    output.Add(ActivityPermissions.ReadKey);
                    break;

                case Permission.ApplicationsUndo:
                    output.Add(UndoPermissions.ApplicationsUndoKey);
                    break;
                case Permission.AccountsUndo:
                    output.Add(UndoPermissions.AccountsUndoKey);
                    break;
                case Permission.IdentitiesUndo:
                    output.Add(UndoPermissions.IdentitiesUndoKey);
                    break;
                case Permission.DataStoresUndo:
                    output.Add(UndoPermissions.DataStoresUndoKey);
                    break;
                case Permission.PlatformsUndo:
                    output.Add(UndoPermissions.PlatformsUndoKey);
                    break;
                case Permission.EnvironmentsUndo:
                    output.Add(UndoPermissions.EnvironmentsUndoKey);
                    break;
                case Permission.ExternalResourcesUndo:
                    output.Add(UndoPermissions.ExternalResourcesUndoKey);
                    break;
                case Permission.MessageBrokersUndo:
                    output.Add(UndoPermissions.MessageBrokersUndoKey);
                    break;
                case Permission.TagsUndo:
                    output.Add(UndoPermissions.TagsUndoKey);
                    break;
                case Permission.PositionsUndo:
                    output.Add(UndoPermissions.PositionsUndoKey);
                    break;
                case Permission.ResponsibilitiesUndo:
                    output.Add(UndoPermissions.ResponsibilitiesUndoKey);
                    break;
                case Permission.RisksUndo:
                    output.Add(UndoPermissions.RisksUndoKey);
                    break;
                case Permission.SecretProvidersUndo:
                    output.Add(UndoPermissions.SecretProvidersUndoKey);
                    break;
                case Permission.SqlIntegrationsUndo:
                    output.Add(UndoPermissions.SqlIntegrationsUndoKey);
                    break;
                case Permission.KumaIntegrationsUndo:
                    output.Add(UndoPermissions.KumaIntegrationsUndoKey);
                    break;
                case Permission.SecurityUndo:
                    output.Add(UndoPermissions.SecurityUndoKey);
                    break;
                case Permission.ConfigurationUndo:
                    output.Add(UndoPermissions.ConfigurationUndoKey);
                    break;

                case Permission.UsersRead:
                    output.Add(UserAccountPermissions.ReadKey);
                    break;
                case Permission.UsersCreate:
                    output.Add(UserAccountPermissions.CreateKey);
                    break;
                case Permission.UsersUpdate:
                    output.Add(UserAccountPermissions.UpdateKey);
                    output.Add(SecuritySettingsPermissions.UpdateSettingsKey);
                    break;
                case Permission.UsersDelete:
                    output.Add(UserAccountPermissions.DeleteKey);
                    output.Add(UserAccountPermissions.ResetPasswordKey);
                    break;

                case Permission.RolesRead:
                    output.Add(RolePermissions.ReadKey);
                    break;
                case Permission.RolesCreate:
                    output.Add(RolePermissions.CreateKey);
                    break;
                case Permission.RolesUpdate:
                    output.Add(RolePermissions.UpdateKey);
                    output.Add(RolePermissions.AssignKey);
                    break;
                case Permission.RolesDelete:
                    output.Add(RolePermissions.DeleteKey);
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
