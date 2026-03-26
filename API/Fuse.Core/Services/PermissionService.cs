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
    public static readonly Guid DefaultAdminRoleId = BuiltInRoles.AdminRoleId;
    public static readonly Guid DefaultReaderRoleId = BuiltInRoles.ReaderRoleId;

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
        // If user has legacy Admin role or is assigned to the built-in Admin role, give all permissions
        if (user.Role == SecurityRole.Admin || user.RoleIds.Contains(DefaultAdminRoleId))
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
            Permission.MessageBrokersRead,
            Permission.PositionsRead,
            Permission.ResponsibilitiesRead,
            Permission.RisksRead,
            Permission.ActivityRead,
            Permission.UsersRead,
            Permission.RolesRead
        };
    }

    /// <summary>
    /// Check if user has admin privileges via legacy Admin role OR membership in default Admin role
    /// </summary>
    public async Task<bool> IsUserAdminAsync(SecurityUser? user, CancellationToken cancellationToken = default)
    {
        if (user is null) return false;
        if (user.Role == SecurityRole.Admin) return true; // Legacy check
        if (user.RoleIds.Contains(DefaultAdminRoleId)) return true; // Role membership check
        return false;
    }
}
