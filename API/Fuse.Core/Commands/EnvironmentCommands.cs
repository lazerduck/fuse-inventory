using Microsoft.AspNetCore.Mvc;

namespace Fuse.Core.Commands;

public record CreateEnvironment (
    string Name,
    string? Description,
    HashSet<Guid> TagIds
);

public record UpdateEnvironment (
    Guid Id,
    string Name,
    string? Description,
    HashSet<Guid> TagIds
);

public record DeleteEnvironment (
    Guid Id
    //ToDo - Extend with deletion type (Cascase or Reassign)
);