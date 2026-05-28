using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class AppConfigurationOperationService : IAppConfigurationOperationService
{
    private readonly IFuseStore _fuseStore;
    private readonly IAzureAppConfigurationClient _azureAppConfigurationClient;

    public AppConfigurationOperationService(
        IFuseStore fuseStore,
        IAzureAppConfigurationClient azureAppConfigurationClient)
    {
        _fuseStore = fuseStore;
        _azureAppConfigurationClient = azureAppConfigurationClient;
    }

    public async Task<Result<IReadOnlyList<AppConfigurationEntry>>> ListKeyValuesAsync(
        Guid providerId,
        string? keySearch = null,
        string? keyPrefix = null,
        string? label = null)
    {
        var store = await _fuseStore.GetAsync();
        var provider = store.SecretProviders.FirstOrDefault(p => p.Id == providerId);

        if (provider is null)
            return Result<IReadOnlyList<AppConfigurationEntry>>.Failure($"Secret provider with ID '{providerId}' not found.", ErrorType.NotFound);

        if (!provider.Capabilities.HasFlag(SecretProviderCapabilities.Check))
            return Result<IReadOnlyList<AppConfigurationEntry>>.Failure("This integration does not have Check capability enabled.", ErrorType.Validation);

        if (!SecretProviderEndpointClassifier.IsAppConfigurationEndpoint(provider.VaultUri))
            return Result<IReadOnlyList<AppConfigurationEntry>>.Failure("This integration is not an Azure App Configuration endpoint.", ErrorType.Validation);

        return await _azureAppConfigurationClient.ListKeyValuesAsync(provider, keySearch, keyPrefix, label);
    }
}
