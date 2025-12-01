using Fuse.Core.Models;

namespace Fuse.Core.Responses;

public record SqlIntegrationResponse
(
    Guid Id,
    string Name,
    Guid DataStoreId,
    Guid? AccountId,
    SqlPermissions Permissions,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SqlConnectionTestResult
(
    bool IsSuccessful,
    SqlPermissions Permissions,
    string? ErrorMessage
);

public record SqlDatabasesResponse
(
    IReadOnlyList<string> Databases
);

/// <summary>
/// Response wrapper for cached permissions overview that includes caching metadata.
/// </summary>
public record CachedPermissionsOverviewResponse(
    SqlIntegrationPermissionsOverviewResponse Overview,
    DateTime? CachedAt,
    bool IsCached
);

/// <summary>
/// Response wrapper for cached account SQL status that includes caching metadata.
/// </summary>
public record CachedAccountSqlStatusResponse(
    AccountSqlStatusResponse Status,
    DateTime? CachedAt,
    bool IsCached
);
