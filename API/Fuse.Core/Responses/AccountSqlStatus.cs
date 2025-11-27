using Fuse.Core.Models;
using Fuse.Core.Interfaces;

namespace Fuse.Core.Responses;

/// <summary>
/// Represents a single permission entry showing configured vs actual state.
/// </summary>
public record SqlPermissionComparison(
    string? Database,
    string? Schema,
    HashSet<Privilege> ConfiguredPrivileges,
    HashSet<Privilege> ActualPrivileges,
    HashSet<Privilege> MissingPrivileges,
    HashSet<Privilege> ExtraPrivileges
);

/// <summary>
/// Status indicating if permissions are in sync between Fuse configuration and SQL.
/// </summary>
public enum SyncStatus
{
    InSync,
    DriftDetected,
    MissingPrincipal,
    Error,
    NotApplicable
}

/// <summary>
/// Response DTO for the account SQL status endpoint.
/// Shows the difference between what Fuse expects and what exists in SQL.
/// </summary>
public record AccountSqlStatusResponse(
    Guid AccountId,
    Guid? SqlIntegrationId,
    string? SqlIntegrationName,
    SyncStatus Status,
    string StatusSummary,
    IReadOnlyList<SqlPermissionComparison> PermissionComparisons,
    string? ErrorMessage
);

/// <summary>
/// Permission status for a single account within an integration overview.
/// </summary>
public record SqlAccountPermissionsStatus(
    Guid AccountId,
    string? AccountName,
    string? PrincipalName,
    SyncStatus Status,
    IReadOnlyList<SqlPermissionComparison> PermissionComparisons,
    string? ErrorMessage
);

/// <summary>
/// Represents an orphan SQL principal that exists but is not mapped to any Fuse account.
/// </summary>
public record SqlOrphanPrincipal(
    string PrincipalName,
    IReadOnlyList<SqlActualGrant> ActualPermissions
);

/// <summary>
/// Summary aggregates for the integration permissions overview.
/// </summary>
public record SqlPermissionsOverviewSummary(
    int TotalAccounts,
    int InSyncCount,
    int DriftCount,
    int MissingPrincipalCount,
    int ErrorCount,
    int OrphanPrincipalCount
);

/// <summary>
/// Response DTO for the SQL integration permissions overview endpoint.
/// </summary>
public record SqlIntegrationPermissionsOverviewResponse(
    Guid IntegrationId,
    string IntegrationName,
    IReadOnlyList<SqlAccountPermissionsStatus> Accounts,
    IReadOnlyList<SqlOrphanPrincipal> OrphanPrincipals,
    SqlPermissionsOverviewSummary Summary,
    string? ErrorMessage
);
