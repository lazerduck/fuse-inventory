using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.SecretProvider;

public interface IAzureAppConfigurationClient
{
    Task<Result> TestConnectionAsync(Uri endpoint, SecretProviderAuthMode authMode, SecretProviderCredentials? credentials);
    Task<Result<IReadOnlyList<AppConfigurationEntry>>> ListKeyValuesAsync(
        Models.SecretProvider provider,
        string? keySearch = null,
        string? keyPrefix = null,
        string? label = null);
    Task<Result<AppConfigurationEntry?>> GetKeyValueAsync(Models.SecretProvider provider, string key, string? label = null);
    Task<Result<AppConfigurationEntry>> SetKeyValueAsync(Models.SecretProvider provider, string key, string? label, string value, string? contentType);
}
