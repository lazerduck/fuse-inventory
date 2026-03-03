using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateIdentity(
    string Name,
    IdentityKind Kind,
    string? Notes,
    Guid? OwnerInstanceId,
    IReadOnlyList<IdentityAssignment>? Assignments,
    HashSet<Guid>? TagIds = null
);

public record UpdateIdentity(
    Guid Id,
    string Name,
    IdentityKind Kind,
    string? Notes,
    Guid? OwnerInstanceId,
    IReadOnlyList<IdentityAssignment>? Assignments,
    HashSet<Guid>? TagIds = null
);

public record DeleteIdentity(
    Guid Id
);

public record CreateIdentityAssignment(
    Guid IdentityId,
    TargetKind TargetKind,
    Guid TargetId,
    string? Role,
    string? Notes
);

public record UpdateIdentityAssignment(
    Guid IdentityId,
    Guid AssignmentId,
    TargetKind TargetKind,
    Guid TargetId,
    string? Role,
    string? Notes
);

public record DeleteIdentityAssignment(
    Guid IdentityId,
    Guid AssignmentId
);

public record CloneIdentity(
    Guid SourceId,
    IReadOnlyList<Guid> TargetOwnerInstanceIds
);
