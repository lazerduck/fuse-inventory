using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

/// <summary>
/// Service for managing permissions and authorization checks
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly ISecurityService _securityService;

    // Predefined role IDs for default roles
    public static readonly Guid DefaultAdminRoleId = new Guid("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DefaultReaderRoleId = new Guid("00000000-0000-0000-0000-000000000002");

    // Mapping of controller actions to required permissions
    private static readonly Dictionary<string, Permission> _actionPermissionMap = new()
    {
        // Application Controller
        ["Application.GetAll.GET"] = Permission.ApplicationsRead,
        ["Application.Get.GET"] = Permission.ApplicationsRead,
        ["Application.Create.POST"] = Permission.ApplicationsCreate,
        ["Application.Update.PUT"] = Permission.ApplicationsUpdate,
        ["Application.Update.PATCH"] = Permission.ApplicationsUpdate,
        ["Application.Delete.DELETE"] = Permission.ApplicationsDelete,

        // Account Controller
        ["Account.GetAll.GET"] = Permission.AccountsRead,
        ["Account.Get.GET"] = Permission.AccountsRead,
        ["Account.Create.POST"] = Permission.AccountsCreate,
        ["Account.Update.PUT"] = Permission.AccountsUpdate,
        ["Account.Update.PATCH"] = Permission.AccountsUpdate,
        ["Account.Delete.DELETE"] = Permission.AccountsDelete,
        ["Account.ApplyGrants.POST"] = Permission.SqlGrantsApply,

        // Identity Controller
        ["Identity.GetAll.GET"] = Permission.IdentitiesRead,
        ["Identity.Get.GET"] = Permission.IdentitiesRead,
        ["Identity.Create.POST"] = Permission.IdentitiesCreate,
        ["Identity.Update.PUT"] = Permission.IdentitiesUpdate,
        ["Identity.Update.PATCH"] = Permission.IdentitiesUpdate,
        ["Identity.Delete.DELETE"] = Permission.IdentitiesDelete,

        // DataStore Controller
        ["DataStore.GetAll.GET"] = Permission.DataStoresRead,
        ["DataStore.Get.GET"] = Permission.DataStoresRead,
        ["DataStore.Create.POST"] = Permission.DataStoresCreate,
        ["DataStore.Update.PUT"] = Permission.DataStoresUpdate,
        ["DataStore.Update.PATCH"] = Permission.DataStoresUpdate,
        ["DataStore.Delete.DELETE"] = Permission.DataStoresDelete,

        // Platform Controller
        ["Platform.GetAll.GET"] = Permission.PlatformsRead,
        ["Platform.Get.GET"] = Permission.PlatformsRead,
        ["Platform.Create.POST"] = Permission.PlatformsCreate,
        ["Platform.Update.PUT"] = Permission.PlatformsUpdate,
        ["Platform.Update.PATCH"] = Permission.PlatformsUpdate,
        ["Platform.Delete.DELETE"] = Permission.PlatformsDelete,

        // Environment Controller
        ["Environment.GetAll.GET"] = Permission.EnvironmentsRead,
        ["Environment.Get.GET"] = Permission.EnvironmentsRead,
        ["Environment.Create.POST"] = Permission.EnvironmentsCreate,
        ["Environment.Update.PUT"] = Permission.EnvironmentsUpdate,
        ["Environment.Update.PATCH"] = Permission.EnvironmentsUpdate,
        ["Environment.Delete.DELETE"] = Permission.EnvironmentsDelete,

        // ExternalResource Controller
        ["ExternalResource.GetAll.GET"] = Permission.ExternalResourcesRead,
        ["ExternalResource.Get.GET"] = Permission.ExternalResourcesRead,
        ["ExternalResource.Create.POST"] = Permission.ExternalResourcesCreate,
        ["ExternalResource.Update.PUT"] = Permission.ExternalResourcesUpdate,
        ["ExternalResource.Update.PATCH"] = Permission.ExternalResourcesUpdate,
        ["ExternalResource.Delete.DELETE"] = Permission.ExternalResourcesDelete,

        // Position Controller
        ["Position.GetAll.GET"] = Permission.PositionsRead,
        ["Position.Get.GET"] = Permission.PositionsRead,
        ["Position.Create.POST"] = Permission.PositionsCreate,
        ["Position.Update.PUT"] = Permission.PositionsUpdate,
        ["Position.Update.PATCH"] = Permission.PositionsUpdate,
        ["Position.Delete.DELETE"] = Permission.PositionsDelete,

        // ResponsibilityType Controller
        ["ResponsibilityType.GetAll.GET"] = Permission.ResponsibilitiesRead,
        ["ResponsibilityType.Get.GET"] = Permission.ResponsibilitiesRead,
        ["ResponsibilityType.Create.POST"] = Permission.ResponsibilitiesCreate,
        ["ResponsibilityType.Update.PUT"] = Permission.ResponsibilitiesUpdate,
        ["ResponsibilityType.Update.PATCH"] = Permission.ResponsibilitiesUpdate,
        ["ResponsibilityType.Delete.DELETE"] = Permission.ResponsibilitiesDelete,

        // ResponsibilityAssignment Controller
        ["ResponsibilityAssignment.GetAll.GET"] = Permission.ResponsibilitiesRead,
        ["ResponsibilityAssignment.Get.GET"] = Permission.ResponsibilitiesRead,
        ["ResponsibilityAssignment.Create.POST"] = Permission.ResponsibilitiesCreate,
        ["ResponsibilityAssignment.Update.PUT"] = Permission.ResponsibilitiesUpdate,
        ["ResponsibilityAssignment.Update.PATCH"] = Permission.ResponsibilitiesUpdate,
        ["ResponsibilityAssignment.Delete.DELETE"] = Permission.ResponsibilitiesDelete,

        // Risk Controller
        ["Risk.GetAll.GET"] = Permission.RisksRead,
        ["Risk.Get.GET"] = Permission.RisksRead,
        ["Risk.Create.POST"] = Permission.RisksCreate,
        ["Risk.Update.PUT"] = Permission.RisksUpdate,
        ["Risk.Update.PATCH"] = Permission.RisksUpdate,
        ["Risk.Delete.DELETE"] = Permission.RisksDelete,
        ["Risk.Approve.POST"] = Permission.RisksApprove,

        // SecretProvider Controller (Azure KV)
        ["SecretProvider.GetSecrets.GET"] = Permission.AzureKeyVaultSecretsView,
        ["SecretProvider.Create.POST"] = Permission.AzureKeyVaultConnectionsCreate,
        ["SecretProvider.Delete.DELETE"] = Permission.AzureKeyVaultConnectionsDelete,

        // SqlIntegration Controller
        ["SqlIntegration.Create.POST"] = Permission.SqlConnectionsCreate,
        ["SqlIntegration.Delete.DELETE"] = Permission.SqlConnectionsDelete,

        // KumaIntegration Controller
        ["KumaIntegration.Create.POST"] = Permission.KumaIntegrationsCreate,
        ["KumaIntegration.Delete.DELETE"] = Permission.KumaIntegrationsDelete,

        // Config Controller
        ["Config.Export.GET"] = Permission.ConfigurationExport,
        ["Config.Import.POST"] = Permission.ConfigurationImport,

        // Audit Controller
        ["Audit.GetAll.GET"] = Permission.AuditLogsView,
        ["Audit.Get.GET"] = Permission.AuditLogsView,

        // Security Controller - User Management
        ["Security.GetAccounts.GET"] = Permission.UsersRead,
        ["Security.CreateAccount.POST"] = Permission.UsersCreate,
        ["Security.UpdateUser.PATCH"] = Permission.UsersUpdate,
        ["Security.DeleteUser.DELETE"] = Permission.UsersDelete
    };

    public PermissionService(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    public async Task<bool> HasPermissionAsync(SecurityUser user, Permission permission, CancellationToken cancellationToken = default)
    {
        var permissions = await GetUserPermissionsAsync(user, cancellationToken);
        return permissions.Contains(permission);
    }

    public async Task<IReadOnlyList<Permission>> GetUserPermissionsAsync(SecurityUser user, CancellationToken cancellationToken = default)
    {
        // If user has legacy Admin role, give all permissions
        if (user.Role == SecurityRole.Admin)
        {
            return GetAllPermissions();
        }

        // If user has legacy Reader role and no new roles, give read permissions
        if (user.Role == SecurityRole.Reader && !user.RoleIds.Any())
        {
            return GetDefaultReaderRole().Permissions;
        }

        // Get permissions from assigned roles
        var state = await _securityService.GetSecurityStateAsync(cancellationToken);
        var userRoles = state.Roles.Where(r => user.RoleIds.Contains(r.Id)).ToList();
        
        var permissions = new HashSet<Permission>();
        foreach (var role in userRoles)
        {
            foreach (var permission in role.Permissions)
            {
                permissions.Add(permission);
            }
        }

        return permissions.ToList();
    }

    public Permission? GetRequiredPermission(string controllerName, string actionName, string httpMethod)
    {
        var key = $"{controllerName}.{actionName}.{httpMethod}";
        return _actionPermissionMap.TryGetValue(key, out var permission) ? permission : null;
    }

    public Role GetDefaultAdminRole()
    {
        return new Role(
            DefaultAdminRoleId,
            "Administrator",
            "Full access to all features and settings",
            GetAllPermissions(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
    }

    public Role GetDefaultReaderRole()
    {
        return new Role(
            DefaultReaderRoleId,
            "Reader",
            "Read-only access to all resources",
            GetReadOnlyPermissions(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
    }

    public async Task<IReadOnlyList<Role>> EnsureDefaultRolesAsync(CancellationToken cancellationToken = default)
    {
        var state = await _securityService.GetSecurityStateAsync(cancellationToken);
        var roles = state.Roles.ToList();
        
        var hasAdminRole = roles.Any(r => r.Id == DefaultAdminRoleId);
        var hasReaderRole = roles.Any(r => r.Id == DefaultReaderRoleId);

        if (!hasAdminRole)
        {
            roles.Add(GetDefaultAdminRole());
        }

        if (!hasReaderRole)
        {
            roles.Add(GetDefaultReaderRole());
        }

        return roles;
    }

    private static IReadOnlyList<Permission> GetAllPermissions()
    {
        return Enum.GetValues<Permission>().ToList();
    }

    private static IReadOnlyList<Permission> GetReadOnlyPermissions()
    {
        return new List<Permission>
        {
            Permission.ApplicationsRead,
            Permission.AccountsRead,
            Permission.IdentitiesRead,
            Permission.DataStoresRead,
            Permission.PlatformsRead,
            Permission.EnvironmentsRead,
            Permission.ExternalResourcesRead,
            Permission.PositionsRead,
            Permission.ResponsibilitiesRead,
            Permission.RisksRead,
            Permission.UsersRead,
            Permission.RolesRead
        };
    }
}
