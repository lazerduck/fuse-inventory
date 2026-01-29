namespace Fuse.Core.Models;

/// <summary>
/// Links a Position to a Responsibility Type for an Application, with optional environment scoping.
/// </summary>
public record ResponsibilityAssignment
(
    Guid Id,
    Guid PositionId,
    Guid ResponsibilityTypeId,
    Guid ApplicationId,
    ResponsibilityScope Scope,
    Guid? EnvironmentId,
    string? Notes,
    bool Primary,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Defines the scope of a responsibility assignment
/// </summary>
public enum ResponsibilityScope
{
    /// <summary>
    /// Applies to all environments
    /// </summary>
    All,
    
    /// <summary>
    /// Applies to a specific environment
    /// </summary>
    Environment
}
