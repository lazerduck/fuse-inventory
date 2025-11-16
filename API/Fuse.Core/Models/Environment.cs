namespace Fuse.Core.Models;

public record EnvironmentInfo
(
    Guid Id,
    string Name,
    string? Description,
    HashSet<Guid> TagIds,
    bool AutoCreateInstances = false,
    string? BaseUriTemplate = null,
    string? HealthUriTemplate = null,
    string? OpenApiUriTemplate = null
);