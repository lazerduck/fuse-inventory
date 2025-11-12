 

namespace Fuse.Core.Commands;

public record CreateEnvironment (
    string Name,
    string? Description,
    HashSet<Guid>? TagIds = null
);

public record UpdateEnvironment (
    Guid Id,
    string Name,
    string? Description,
    HashSet<Guid>? TagIds = null
);

public record DeleteEnvironment (
    Guid Id
    //ToDo - Extend with deletion type (Cascase or Reassign)
);
