namespace Fuse.Core.Models;

public record SecretProvider
(
    Guid Id,
    string Name,
    Uri VaultUri,
    SecretProviderAuthMode AuthMode,
    SecretProviderCredentials? Credentials,
    SecretProviderCapabilities Capabilities,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public enum SecretProviderAuthMode
{
    ManagedIdentity,
    ClientSecret
}

public record SecretProviderCredentials
(
    string? TenantId,
    string? ClientId,
    string? ClientSecret
);

[Flags]
public enum SecretProviderCapabilities
{
    None = 0,
    Check = 1,
    Create = 2,
    Rotate = 4,
    Read = 8
}

public record SecretBinding
(
    SecretBindingKind Kind,
    string? PlainReference,
    AzureKeyVaultBinding? AzureKeyVault
);

public enum SecretBindingKind
{
    None,
    PlainReference,
    AzureKeyVault
}

public record AzureKeyVaultBinding
(
    Guid ProviderId,
    string SecretName,
    string? Version
);

public record SecretMetadata
(
    string Name,
    bool Enabled,
    DateTimeOffset? UpdatedOn,
    string? ContentType
);
