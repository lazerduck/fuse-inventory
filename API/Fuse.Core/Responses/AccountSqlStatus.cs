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

/// <summary>
/// Result of a single GRANT or REVOKE SQL operation.
/// </summary>
public record DriftResolutionOperation(
    string OperationType,
    string? Database,
    string? Schema,
    Privilege Privilege,
    bool Success,
    string? ErrorMessage
);

/// <summary>
/// Response DTO for drift resolution endpoint.
/// </summary>
public record ResolveDriftResponse(
    Guid AccountId,
    string? PrincipalName,
    bool Success,
    IReadOnlyList<DriftResolutionOperation> Operations,
    SqlAccountPermissionsStatus UpdatedStatus,
    string? ErrorMessage
);

/// <summary>
/// Result of a SQL account creation operation.
/// </summary>
public record SqlAccountCreationOperation(
    string OperationType,
    string? Database,
    bool Success,
    string? ErrorMessage
);

/// <summary>
/// Specifies how the password was obtained for SQL account creation.
/// </summary>
public enum PasswordSourceUsed
{
    SecretProvider,
    Manual,
    NewSecret
}

/// <summary>
/// Response DTO for SQL account creation endpoint.
/// </summary>
public record CreateSqlAccountResponse(
    Guid AccountId,
    string? PrincipalName,
    bool Success,
    PasswordSourceUsed PasswordSource,
    IReadOnlyList<SqlAccountCreationOperation> Operations,
    SqlAccountPermissionsStatus? UpdatedStatus,
    string? ErrorMessage
);

/// <summary>
/// Result for a single account operation during bulk resolve.
/// </summary>
public record BulkResolveAccountResult(
    Guid AccountId,
    string? AccountName,
    string? PrincipalName,
    string OperationType,
    bool Success,
    string? ErrorMessage,
    SqlAccountPermissionsStatus? UpdatedStatus
);

/// <summary>
/// Summary of bulk resolve operations.
/// </summary>
public record BulkResolveSummary(
    int TotalProcessed,
    int AccountsCreated,
    int DriftsResolved,
    int Skipped,
    int Failed
);

/// <summary>
/// Response DTO for bulk resolve endpoint.
/// </summary>
public record BulkResolveResponse(
    Guid IntegrationId,
    bool Success,
    BulkResolveSummary Summary,
    IReadOnlyList<BulkResolveAccountResult> Results,
    string? ErrorMessage
);

/// <summary>
/// Response DTO for importing actual SQL permissions into Fuse account.
/// </summary>
public record ImportPermissionsResponse(
    Guid AccountId,
    string? PrincipalName,
    bool Success,
    IReadOnlyList<Grant> ImportedGrants,
    SqlAccountPermissionsStatus? UpdatedStatus,
    string? ErrorMessage
);

/// <summary>
/// Response DTO for importing an orphan SQL principal as a new Fuse account.
/// </summary>
public record ImportOrphanPrincipalResponse(
    Guid AccountId,
    string PrincipalName,
    bool Success,
    IReadOnlyList<Grant> ImportedGrants,
    string? ErrorMessage
);
