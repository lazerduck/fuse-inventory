using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IAzureKeyVaultClient
{
    Task<Result> TestConnectionAsync(Uri vaultUri, SecretProviderAuthMode authMode, SecretProviderCredentials? credentials);
    Task<Result> CheckSecretExistsAsync(SecretProvider provider, string secretName);
    Task<Result> CreateSecretAsync(SecretProvider provider, string secretName, string secretValue);
    Task<Result> RotateSecretAsync(SecretProvider provider, string secretName, string newSecretValue);
    Task<Result<string>> ReadSecretAsync(SecretProvider provider, string secretName, string? version = null);
    Task<Result<IReadOnlyList<SecretMetadata>>> ListSecretsAsync(SecretProvider provider);
}
