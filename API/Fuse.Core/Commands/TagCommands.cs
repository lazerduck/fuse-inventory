using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateTag (
    string Name,
    string? Description,
    TagColor? Color
);

public record UpdateTag (
    Guid Id,
    string Name,
    string? Description,
    TagColor? Color
);

public record DeleteTag (
    Guid Id
);