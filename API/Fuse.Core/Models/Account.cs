namespace Fuse.Core.Models;

public record Account
(
    Guid Id,
    // Target
    Guid TargetId,
    TargetKind TargetKind,

    // Connection
    AuthKind AuthKind,
    string SecretRef,
    string? UserName,
    Dictionary<string, string>? Parameters,
    IReadOnlyList<Grant> Grants,

    // Metadata
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public enum AuthKind { None, UserPassword, ApiKey, BearerToken, OAuthClient, ManagedIdentity, Certificate, Other }

public record Grant
(
    string? Database,
    string? Schema,
    HashSet<Privilege> Privileges
);

public enum Privilege { Select, Insert, Update, Delete, Execute, Connect, Alter, Control }