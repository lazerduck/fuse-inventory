using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.Helpers;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class AppConfigurationOperationServiceTests
{
    private static InMemoryFuseStore NewStore(IEnumerable<SecretProvider>? providers = null)
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
            SecretProviders: (providers ?? Array.Empty<SecretProvider>()).ToArray(),
            SqlIntegrations: Array.Empty<SqlIntegration>(), Positions: Array.Empty<Position>(), ResponsibilityTypes: Array.Empty<ResponsibilityType>(), ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>()),
            SecurityContextHelper.Get
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task ListKeyValuesAsync_WithNonAppConfigurationProvider_ReturnsValidationError()
    {
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "kv",
            new Uri("https://example.vault.azure.net/"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check,
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var store = NewStore(new[] { provider });
        var mockClient = new Mock<IAzureAppConfigurationClient>(MockBehavior.Strict);
        var service = new AppConfigurationOperationService(store, mockClient.Object);

        var result = await service.ListKeyValuesAsync(provider.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("not an Azure App Configuration", result.Error);
    }

    [Fact]
    public async Task ListKeyValuesAsync_WithAppConfigurationProvider_DelegatesToClient()
    {
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "app-config",
            new Uri("https://example.azconfig.io/"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check,
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var store = NewStore(new[] { provider });
        var entries = new List<AppConfigurationEntry>
        {
            new("Shared:Setting", "value", null, null, null, false, false, null)
        };
        var mockClient = new Mock<IAzureAppConfigurationClient>();
        mockClient.Setup(c => c.ListKeyValuesAsync(provider, "Setting", "Shared:", "prod"))
            .ReturnsAsync(Result<IReadOnlyList<AppConfigurationEntry>>.Success(entries));

        var service = new AppConfigurationOperationService(store, mockClient.Object);
        var result = await service.ListKeyValuesAsync(provider.Id, "Setting", "Shared:", "prod");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }
}
