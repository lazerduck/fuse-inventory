namespace Fuse.Core.Models;

public record ExternalResource
(
    Guid Id,
    string Name,
    string? Description,
    Uri? ResourceUri,
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);