using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.SecretProvider;

public interface IAzureKeyVaultClient
{
    Task<Result> TestConnectionAsync(Uri vaultUri, SecretProviderAuthMode authMode, SecretProviderCredentials? credentials);
    Task<Result> CheckSecretExistsAsync(Models.SecretProvider provider, string secretName);
    Task<Result> CreateSecretAsync(Models.SecretProvider provider, string secretName, string secretValue);
    Task<Result> RotateSecretAsync(Models.SecretProvider provider, string secretName, string newSecretValue);
    Task<Result<string>> ReadSecretAsync(Models.SecretProvider provider, string secretName, string? version = null);
    Task<Result<IReadOnlyList<SecretMetadata>>> ListSecretsAsync(Models.SecretProvider provider);
}
