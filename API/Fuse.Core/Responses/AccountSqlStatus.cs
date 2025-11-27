using Fuse.Core.Models;

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
