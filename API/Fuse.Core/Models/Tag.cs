namespace Fuse.Core.Models;

public record Tag
(
    Guid Id,
    string Name,
    string? Description,
    string? Color
);