namespace Fuse.Core.Models;

public record DataStore
(
    Guid Id,
    string Name,
    string? Description,
    string Kind,
    Guid EnvironmentId,
    Guid? PlatformId,
    Uri? ConnectionUri,
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);