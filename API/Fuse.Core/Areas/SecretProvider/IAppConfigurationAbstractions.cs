using Azure;
using Azure.Core;
using Azure.Data.AppConfiguration;

namespace Fuse.Core.Areas.SecretProvider;

public interface IAppConfigurationClient
{
    AsyncPageable<ConfigurationSetting> GetConfigurationSettingsAsync(string keyFilter = "*", string labelFilter = "*", CancellationToken ct = default);
    Task<ConfigurationSetting?> GetConfigurationSettingAsync(string key, string? label = null, CancellationToken ct = default);
    Task<ConfigurationSetting> SetConfigurationSettingAsync(ConfigurationSetting setting, CancellationToken ct = default);
}

public interface IAppConfigurationClientFactory
{
    IAppConfigurationClient Create(Uri endpoint, TokenCredential credential);
}
