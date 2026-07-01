using Azure;
using Azure.Security.KeyVault.Secrets;
using Azure.Core;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.SecretProvider;

// Abstraction over Azure SecretClient for unit testing
public interface IKeyVaultSecretClient
{
    Task<Response<KeyVaultSecret>> GetSecretAsync(string name, CancellationToken ct = default);
    Task<Response<KeyVaultSecret>> GetSecretVersionAsync(string name, string version, CancellationToken ct = default);
    Task<Response<KeyVaultSecret>> SetSecretAsync(string name, string value, CancellationToken ct = default);
    AsyncPageable<SecretProperties> GetPropertiesOfSecretsAsync(CancellationToken ct = default);
}

public interface IKeyVaultSecretClientFactory
{
    IKeyVaultSecretClient Create(Uri vaultUri, TokenCredential credential);
}

public interface ITokenCredentialFactory
{
    TokenCredential Create(SecretProviderAuthMode mode, SecretProviderCredentials? credentials);
}
