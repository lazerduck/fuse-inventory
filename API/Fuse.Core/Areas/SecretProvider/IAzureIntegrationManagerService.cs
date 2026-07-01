using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.SecretProvider;

public interface IAzureIntegrationManagerService
{
    Task<AzureIntegrationManager?> GetManagerAsync();
    Task<SecretProviderCredentials?> GetClientSecretCredentialsAsync();
    Task<Result<AzureIntegrationManager>> UpdateClientSecretCredentialsAsync(SecretProviderCredentials credentials);
}
