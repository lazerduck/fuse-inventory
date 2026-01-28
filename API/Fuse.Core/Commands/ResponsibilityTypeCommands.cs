namespace Fuse.Core.Commands;

public record CreateResponsibilityType (
    string Name,
    string? Description
);

public record UpdateResponsibilityType (
    Guid Id,
    string Name,
    string? Description
);

public record DeleteResponsibilityType (
    Guid Id
);
