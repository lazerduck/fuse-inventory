using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class AzureIntegrationManagerService : IAzureIntegrationManagerService
{
    private readonly IFuseStore _fuseStore;

    public AzureIntegrationManagerService(IFuseStore fuseStore)
    {
        _fuseStore = fuseStore;
    }

    public async Task<AzureIntegrationManager?> GetManagerAsync()
        => (await _fuseStore.GetAsync()).AzureIntegrationManager;

    public async Task<SecretProviderCredentials?> GetClientSecretCredentialsAsync()
        => (await GetManagerAsync())?.ClientSecretCredentials;

    public async Task<Result<AzureIntegrationManager>> UpdateClientSecretCredentialsAsync(SecretProviderCredentials credentials)
    {
        if (!HasCompleteClientSecretCredentials(credentials))
        {
            return Result<AzureIntegrationManager>.Failure(
                "Client secret credentials require TenantId, ClientId, and ClientSecret.",
                ErrorType.Validation
            );
        }

        var manager = new AzureIntegrationManager(credentials, DateTime.UtcNow);
        await _fuseStore.UpdateAsync(snapshot => snapshot with { AzureIntegrationManager = manager });
        return Result<AzureIntegrationManager>.Success(manager);
    }

    private static bool HasCompleteClientSecretCredentials(SecretProviderCredentials? credentials)
        => credentials is not null
           && !string.IsNullOrWhiteSpace(credentials.TenantId)
           && !string.IsNullOrWhiteSpace(credentials.ClientId)
           && !string.IsNullOrWhiteSpace(credentials.ClientSecret);
}
