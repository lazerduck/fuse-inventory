namespace Fuse.Core.Models;

public record KumaIntegration
(
    Guid Id,
    string? Name,
    IReadOnlyList<Guid> EnvironmentIds,
    Guid? PlatformId,
    Guid? AccountId,
    Uri Uri,
    string ApiKey,
    DateTime CreatedAt,
    DateTime UpdatedAt
);