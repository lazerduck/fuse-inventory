using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

/// <summary>
/// Service for managing permissions and authorization checks
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Check if a user has a specific permission based on their roles
    /// </summary>
    Task<bool> HasPermissionAsync(SecurityUser user, Permission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all permissions for a user based on their roles
    /// </summary>
    Task<IReadOnlyList<Permission>> GetUserPermissionsAsync(SecurityUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the required permission for a controller action
    /// </summary>
    Permission? GetRequiredPermission(string controllerName, string actionName, string httpMethod);

    /// <summary>
    /// Get default Admin role with all permissions
    /// </summary>
    Role GetDefaultAdminRole();

    /// <summary>
    /// Get default Reader role with read-only permissions
    /// </summary>
    Role GetDefaultReaderRole();

    /// <summary>
    /// Ensure default roles exist in the system
    /// </summary>
    Task<IReadOnlyList<Role>> EnsureDefaultRolesAsync(CancellationToken cancellationToken = default);
}
