namespace Fuse.Core.Models;

/// <summary>
/// Represents how a Position is accountable for a system.
/// Examples: Technical Owner, Business Owner, Lead Support, Project Lead, Data Controller
/// </summary>
public record ResponsibilityType
(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
