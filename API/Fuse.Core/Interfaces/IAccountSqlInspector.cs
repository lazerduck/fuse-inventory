using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Interfaces;

/// <summary>
/// Represents actual permissions found for a SQL principal.
/// </summary>
public record SqlPrincipalPermissions(
    string? PrincipalName,
    bool Exists,
    IReadOnlyList<SqlActualGrant> Grants
);

/// <summary>
/// Represents a single actual grant found in SQL.
/// </summary>
public record SqlActualGrant(
    string? Database,
    string? Schema,
    HashSet<Privilege> Privileges
);

/// <summary>
/// Abstraction for inspecting SQL principals and their permissions.
/// </summary>
public interface IAccountSqlInspector
{
    /// <summary>
    /// Queries SQL for the actual permissions of a principal.
    /// </summary>
    /// <param name="sqlIntegration">The SQL integration containing connection info.</param>
    /// <param name="principalName">The SQL principal name to inspect (typically the account username).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The actual permissions found for the principal, or error information.</returns>
    Task<(bool IsSuccessful, SqlPrincipalPermissions? Permissions, string? ErrorMessage)> GetPrincipalPermissionsAsync(
        SqlIntegration sqlIntegration,
        string principalName,
        CancellationToken ct = default);

    /// <summary>
    /// Applies GRANT and REVOKE statements to resolve permission drift for a principal.
    /// </summary>
    /// <param name="sqlIntegration">The SQL integration containing connection info.</param>
    /// <param name="principalName">The SQL principal name to modify.</param>
    /// <param name="permissionComparisons">The permission comparisons showing what needs to be granted/revoked.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of operations performed with their success/failure status.</returns>
    Task<(bool IsSuccessful, IReadOnlyList<DriftResolutionOperation> Operations, string? ErrorMessage)> ApplyPermissionChangesAsync(
        SqlIntegration sqlIntegration,
        string principalName,
        IReadOnlyList<SqlPermissionComparison> permissionComparisons,
        CancellationToken ct = default);
}
