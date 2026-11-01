using Fuse.Core.Models;

namespace Fuse.Core.Responses;

public record SecretProviderResponse
(
    Guid Id,
    string Name,
    Uri VaultUri,
    SecretProviderAuthMode AuthMode,
    SecretProviderCapabilities Capabilities,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CredentialSource = "ManagedIdentity",
    string IntegrationKind = "KeyVault",
    string? AzureResourceId = null,
    DateTime? LastCheckedAt = null,
    bool? LastCheckSucceeded = null,
    string? LastCheckMessage = null
)
{
    public static SecretProviderResponse From(SecretProvider provider) => new(
        provider.Id, provider.Name, provider.VaultUri, provider.AuthMode, provider.Capabilities,
        provider.CreatedAt, provider.UpdatedAt,
        provider.AuthMode == SecretProviderAuthMode.ManagedIdentity ? "ManagedIdentity" :
            provider.Credentials is { TenantId.Length: > 0, ClientId.Length: > 0, ClientSecret.Length: > 0 } ? "CustomCredentials" : "SharedCredentials",
        Areas.SecretProvider.SecretProviderEndpointClassifier.IsAppConfigurationEndpoint(provider.VaultUri) ? "AppConfiguration" : "KeyVault",
        provider.AzureResourceId, provider.LastCheckedAt, provider.LastCheckSucceeded, provider.LastCheckMessage);
}

public record SecretValueResponse
(
    string Value
);

public record SecretMetadataResponse
(
    string Name,
    bool Enabled,
    DateTimeOffset? UpdatedOn,
    string? ContentType
);

public record AzureIntegrationManagerResponse
(
    bool HasClientSecretCredentials,
    string? TenantId,
    string? ClientId,
    DateTime? UpdatedAt
);

public record AppConfigurationEntryResponse
(
    string Key,
    string? Value,
    string? Label,
    string? ContentType,
    DateTimeOffset? LastModified,
    bool IsLocked,
    bool IsKeyVaultReference,
    string? KeyVaultReferenceUri
);

public record ResolvedAppConfigurationReferenceSecretResponse
(
    string SecretName,
    string? SecretVersion,
    string Value
);
