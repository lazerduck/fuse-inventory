using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.Helpers;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Services;

public class AzureIntegrationManagerServiceTests
{
    private static InMemoryFuseStore NewStore()
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Identities: Array.Empty<Identity>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: Array.Empty<SqlIntegration>(), Positions: Array.Empty<Position>(), ResponsibilityTypes: Array.Empty<ResponsibilityType>(), ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>()),
            SecurityContextHelper.Get
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task UpdateClientSecretCredentialsAsync_WithValidCredentials_PersistsManager()
    {
        var store = NewStore();
        var service = new AzureIntegrationManagerService(store);
        var credentials = new SecretProviderCredentials("tenant-id", "client-id", "client-secret");

        var result = await service.UpdateClientSecretCredentialsAsync(credentials);

        Assert.True(result.IsSuccess);
        var persisted = await service.GetClientSecretCredentialsAsync();
        Assert.Equal("tenant-id", persisted!.TenantId);
        Assert.Equal("client-id", persisted.ClientId);
        Assert.Equal("client-secret", persisted.ClientSecret);
    }

    [Fact]
    public async Task UpdateClientSecretCredentialsAsync_WithIncompleteCredentials_ReturnsFailure()
    {
        var store = NewStore();
        var service = new AzureIntegrationManagerService(store);

        var result = await service.UpdateClientSecretCredentialsAsync(new SecretProviderCredentials("tenant-id", null, null));

        Assert.False(result.IsSuccess);
        Assert.Contains("Client secret credentials require", result.Error);
    }
}
