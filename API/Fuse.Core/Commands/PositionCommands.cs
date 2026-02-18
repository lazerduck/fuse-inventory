namespace Fuse.Core.Commands;

public record CreatePosition (
    string Name,
    string? Description,
    HashSet<Guid>? TagIds
);

public record UpdatePosition (
    Guid Id,
    string Name,
    string? Description,
    HashSet<Guid>? TagIds
);

public record DeletePosition (
    Guid Id
);
