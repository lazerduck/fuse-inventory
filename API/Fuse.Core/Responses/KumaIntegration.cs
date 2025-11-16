namespace Fuse.Core.Responses;

public record KumaIntegrationResponse
(
    Guid Id,
    string? Name,
    IReadOnlyList<Guid> EnvironmentIds,
    Guid? PlatformId,
    Guid? AccountId,
    Uri Uri,
    DateTime CreatedAt,
    DateTime UpdatedAt
);