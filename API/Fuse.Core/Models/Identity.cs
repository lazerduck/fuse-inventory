namespace Fuse.Core.Models;

public record Identity
(
    Guid Id,
    string Name,
    IdentityKind Kind,
    string? Notes,
    Guid? OwnerInstanceId,
    IReadOnlyList<IdentityAssignment> Assignments,
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public enum IdentityKind
{
    AzureManagedIdentity,
    KubernetesServiceAccount,
    AwsIamRole,
    Custom
}

public record IdentityAssignment
(
    Guid Id,
    TargetKind TargetKind,
    Guid TargetId,
    string? Role,
    string? Notes
);
