namespace Fuse.Core.Models;

/// <summary>
/// Represents a standing organisational function or title, not an individual person.
/// Examples: Head of IT, Infrastructure Team, Data Protection Officer, External Vendor
/// </summary>
public record Position
(
    Guid Id,
    string Name,
    string? Description,
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
