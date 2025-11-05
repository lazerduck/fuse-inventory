namespace Fuse.Core.Models;

public record Tag
(
    Guid Id,
    string Name,
    string? Description,
    TagColor? Color
);

public enum TagColor
{
    Red,
    Green,
    Blue,
    Yellow,
    Purple,
    Orange,
    Teal,
    Gray
}