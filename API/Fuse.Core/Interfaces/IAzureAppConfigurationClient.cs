using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IAzureAppConfigurationClient
{
    Task<Result> TestConnectionAsync(Uri endpoint, SecretProviderAuthMode authMode, SecretProviderCredentials? credentials);
    Task<Result<IReadOnlyList<AppConfigurationEntry>>> ListKeyValuesAsync(
        SecretProvider provider,
        string? keySearch = null,
        string? keyPrefix = null,
        string? label = null);
}
