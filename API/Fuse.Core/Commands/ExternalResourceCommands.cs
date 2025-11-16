 

namespace Fuse.Core.Commands;

public record CreateExternalResource(
    string Name,
    string? Description,
    Uri? ResourceUri,
    HashSet<Guid>? TagIds = null
);

public record UpdateExternalResource(
    Guid Id,
    string Name,
    string? Description,
    Uri? ResourceUri,
    HashSet<Guid>? TagIds = null
);

public record DeleteExternalResource(
    Guid Id
);
