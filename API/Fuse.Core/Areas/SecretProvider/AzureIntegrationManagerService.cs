using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.SecretProvider;

public class AzureIntegrationManagerService : IAzureIntegrationManagerService
{
    private readonly IFuseStore _fuseStore;

    private readonly Fuse.Core.Areas.AzureMetadata.IAzureProfileProtector? _protector;
    public AzureIntegrationManagerService(IFuseStore fuseStore, Fuse.Core.Areas.AzureMetadata.IAzureProfileProtector? protector = null)
    {
        _fuseStore = fuseStore;
        _protector = protector;
    }

    public async Task<AzureIntegrationManager?> GetManagerAsync()
        => (await _fuseStore.GetAsync()).AzureIntegrationManager;

    public async Task<SecretProviderCredentials?> GetClientSecretCredentialsAsync()
        => (await GetManagerAsync())?.ClientSecretCredentials;

    public async Task<SecretProviderCredentials?> GetProfileCredentialsAsync(Guid id, string resourceId)
    {
        var profile = (await GetManagerAsync())?.CredentialProfiles?.FirstOrDefault(p => p.Id == id &&
            p.ResourceId.Equals(resourceId.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
        if (profile is null || _protector is null) return null;
        try { return new(profile.TenantId.ToString(), profile.ClientId.ToString(), _protector.Unprotect(id, profile.ProtectedSecret)); }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }

    public async Task<Result<AzureIntegrationManager>> UpdateClientSecretCredentialsAsync(SecretProviderCredentials credentials)
    {
        if (!HasCompleteClientSecretCredentials(credentials))
        {
            return Result<AzureIntegrationManager>.Failure(
                "Client secret credentials require TenantId, ClientId, and ClientSecret.",
                ErrorType.Validation
            );
        }

        AzureIntegrationManager manager = null!;
        await _fuseStore.UpdateAsync(snapshot =>
        {
            manager = (snapshot.AzureIntegrationManager ?? new AzureIntegrationManager(null, DateTime.UtcNow)) with
            {
                ClientSecretCredentials = credentials,
                UpdatedAt = DateTime.UtcNow
            };
            return snapshot with
            {
                AzureIntegrationManager = manager,
                SecretProviders = snapshot.SecretProviders.Select(p => p.AuthMode == SecretProviderAuthMode.ClientSecret &&
                    !HasCompleteClientSecretCredentials(p.Credentials)
                    ? p with { LastCheckedAt = null, LastCheckSucceeded = null, LastCheckMessage = null } : p).ToList()
            };
        });
        return Result<AzureIntegrationManager>.Success(manager);
    }

    private static bool HasCompleteClientSecretCredentials(SecretProviderCredentials? credentials)
        => credentials is not null
           && !string.IsNullOrWhiteSpace(credentials.TenantId)
           && !string.IsNullOrWhiteSpace(credentials.ClientId)
           && !string.IsNullOrWhiteSpace(credentials.ClientSecret);
}
