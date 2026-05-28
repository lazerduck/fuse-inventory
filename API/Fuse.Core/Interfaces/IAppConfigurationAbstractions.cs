using Azure;
using Azure.Core;
using Azure.Data.AppConfiguration;

namespace Fuse.Core.Interfaces;

public interface IAppConfigurationClient
{
    AsyncPageable<ConfigurationSetting> GetConfigurationSettingsAsync(string keyFilter = "*", string labelFilter = "*", CancellationToken ct = default);
}

public interface IAppConfigurationClientFactory
{
    IAppConfigurationClient Create(Uri endpoint, TokenCredential credential);
}
