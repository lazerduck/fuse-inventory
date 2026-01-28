using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateResponsibilityAssignment (
    Guid PositionId,
    Guid ResponsibilityTypeId,
    Guid ApplicationId,
    ResponsibilityScope Scope,
    Guid? EnvironmentId,
    string? Notes,
    bool Primary
);

public record UpdateResponsibilityAssignment (
    Guid Id,
    Guid PositionId,
    Guid ResponsibilityTypeId,
    Guid ApplicationId,
    ResponsibilityScope Scope,
    Guid? EnvironmentId,
    string? Notes,
    bool Primary
);

public record DeleteResponsibilityAssignment (
    Guid Id
);
