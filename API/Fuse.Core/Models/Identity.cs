namespace Fuse.Core.Models;

/// <summary>
/// Represents an app-owned identity used for authentication with targets.
/// Unlike Accounts (created by targets), Identities represent what the application
/// has been granted permission to access (e.g., Azure Managed Identity, K8s ServiceAccount).
/// </summary>
/// <param name="OwnerInstanceId">
/// Optional owner instance. If null, the identity is shared/global and can be used
/// by any instance's dependencies. If set, only that instance can use this identity.
/// </param>
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

/// <summary>
/// The kind of platform identity this represents.
/// </summary>
public enum IdentityKind
{
    AzureManagedIdentity,
    KubernetesServiceAccount,
    AwsIamRole,
    Custom
}

/// <summary>
/// Represents a permission assignment from an identity to a target.
/// This is descriptive documentation only - Fuse does not enforce or validate these assignments.
/// </summary>
public record IdentityAssignment
(
    Guid Id,
    TargetKind TargetKind,
    Guid TargetId,
    string? Role,
    string? Notes
);
