namespace Fuse.Core.Models;

public record Account
(
    Guid Id,
    // Target
    Guid TargetId,
    TargetKind TargetKind,

    // Connection
    AuthKind AuthKind,
    SecretBinding SecretBinding,
    string? UserName,
    Dictionary<string, string>? Parameters,
    IReadOnlyList<Grant> Grants,

    // Metadata
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    // Legacy property for backward compatibility during migration
    public string SecretRef => SecretBinding.Kind switch
    {
        SecretBindingKind.PlainReference => SecretBinding.PlainReference ?? string.Empty,
        SecretBindingKind.AzureKeyVault => $"akv:{SecretBinding.AzureKeyVault?.SecretName}",
        _ => string.Empty
    };
};

public enum AuthKind { None, UserPassword, ApiKey, BearerToken, OAuthClient, ManagedIdentity, Certificate, Other }

public record Grant
(
    Guid Id,
    string? Database,
    string? Schema,
    HashSet<Privilege> Privileges
);

public enum Privilege { Select, Insert, Update, Delete, Execute, Connect, Alter, Control }