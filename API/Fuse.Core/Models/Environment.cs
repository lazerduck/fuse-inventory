namespace Fuse.Core.Models;

public record EnvironmentInfo
(
    Guid Id,
    string Name,
    string? Description,
    HashSet<Guid> TagIds
);