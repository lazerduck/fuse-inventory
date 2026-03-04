namespace Fuse.Core.Models;

public record MessageBroker
(
    Guid Id,
    string Name,
    string? Description,
    string Kind,
    Guid EnvironmentId,
    Uri? ConnectionUri,
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
