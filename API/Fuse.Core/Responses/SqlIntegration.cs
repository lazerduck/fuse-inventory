using Fuse.Core.Models;

namespace Fuse.Core.Responses;

public record SqlIntegrationResponse
(
    Guid Id,
    string Name,
    Guid DataStoreId,
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
