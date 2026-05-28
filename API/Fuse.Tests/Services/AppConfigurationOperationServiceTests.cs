using Fuse.Core.Commands;
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

    private static SecretProvider NewAppConfigProvider(SecretProviderCapabilities capabilities = SecretProviderCapabilities.Check | SecretProviderCapabilities.Create | SecretProviderCapabilities.Rotate) =>
        new(
            Guid.NewGuid(),
            "app-config",
            new Uri("https://example.azconfig.io/"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            capabilities,
            DateTime.UtcNow,
            DateTime.UtcNow
        );

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
        var service = new AppConfigurationOperationService(store, mockClient.Object, new FakeAuditService());

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

        var service = new AppConfigurationOperationService(store, mockClient.Object, new FakeAuditService());
        var result = await service.ListKeyValuesAsync(provider.Id, "Setting", "Shared:", "prod");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task SetKeyValueAsync_FailsForNonAppConfigurationProvider()
    {
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "kv",
            new Uri("https://example.vault.azure.net/"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Create | SecretProviderCapabilities.Rotate,
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var store = NewStore(new[] { provider });
        var mockClient = new Mock<IAzureAppConfigurationClient>(MockBehavior.Strict);
        var service = new AppConfigurationOperationService(store, mockClient.Object, new FakeAuditService());

        var command = new SetAppConfigurationValue(provider.Id, "App:Key", null, "value", null);
        var result = await service.SetKeyValueAsync(command, "user", null);

        Assert.False(result.IsSuccess);
        Assert.Contains("not an Azure App Configuration", result.Error);
    }

    [Fact]
    public async Task SetKeyValueAsync_FailsForLockedEntry()
    {
        var provider = NewAppConfigProvider();
        var store = NewStore(new[] { provider });

        var lockedEntry = new AppConfigurationEntry("App:Key", "old-value", null, null, null, IsLocked: true, IsKeyVaultReference: false, null);
        var mockClient = new Mock<IAzureAppConfigurationClient>();
        mockClient.Setup(c => c.GetKeyValueAsync(provider, "App:Key", null))
            .ReturnsAsync(Result<AppConfigurationEntry?>.Success(lockedEntry));

        var service = new AppConfigurationOperationService(store, mockClient.Object, new FakeAuditService());

        var command = new SetAppConfigurationValue(provider.Id, "App:Key", null, "new-value", null);
        var result = await service.SetKeyValueAsync(command, "user", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("locked", result.Error);
    }

    [Fact]
    public async Task SetKeyValueAsync_FailsForKeyVaultReference()
    {
        var provider = NewAppConfigProvider();
        var store = NewStore(new[] { provider });

        var kvRefEntry = new AppConfigurationEntry("App:Key", null, null, null, null, IsLocked: false, IsKeyVaultReference: true, "https://vault.azure.net/secrets/key");
        var mockClient = new Mock<IAzureAppConfigurationClient>();
        mockClient.Setup(c => c.GetKeyValueAsync(provider, "App:Key", null))
            .ReturnsAsync(Result<AppConfigurationEntry?>.Success(kvRefEntry));

        var service = new AppConfigurationOperationService(store, mockClient.Object, new FakeAuditService());

        var command = new SetAppConfigurationValue(provider.Id, "App:Key", null, "new-value", null);
        var result = await service.SetKeyValueAsync(command, "user", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Key Vault reference", result.Error);
    }

    [Fact]
    public async Task SetKeyValueAsync_CreatesNewEntry_AndAudits()
    {
        var provider = NewAppConfigProvider();
        var store = NewStore(new[] { provider });
        var audit = new FakeAuditService();

        var newEntry = new AppConfigurationEntry("App:Key", "value", null, null, DateTimeOffset.UtcNow, false, false, null);
        var mockClient = new Mock<IAzureAppConfigurationClient>();
        mockClient.Setup(c => c.GetKeyValueAsync(provider, "App:Key", null))
            .ReturnsAsync(Result<AppConfigurationEntry?>.Success(null));
        mockClient.Setup(c => c.SetKeyValueAsync(provider, "App:Key", null, "value", null))
            .ReturnsAsync(Result<AppConfigurationEntry>.Success(newEntry));

        var service = new AppConfigurationOperationService(store, mockClient.Object, audit);
        var command = new SetAppConfigurationValue(provider.Id, "App:Key", null, "value", null);
        var result = await service.SetKeyValueAsync(command, "test-user", Guid.Empty);

        Assert.True(result.IsSuccess);
        Assert.Equal("App:Key", result.Value!.Key);
        Assert.Single(audit.Logs);
        Assert.Equal(AuditAction.AppConfigurationKeyValueCreated, audit.Logs[0].Action);
        Assert.Equal(AuditArea.SecretProvider, audit.Logs[0].Area);
        Assert.Equal("test-user", audit.Logs[0].UserName);
    }

    [Fact]
    public async Task SetKeyValueAsync_UpdatesExistingEntry_AndAudits()
    {
        var provider = NewAppConfigProvider();
        var store = NewStore(new[] { provider });
        var audit = new FakeAuditService();

        var existingEntry = new AppConfigurationEntry("App:Key", "old-value", "prod", "text/plain", null, false, false, null);
        var updatedEntry = new AppConfigurationEntry("App:Key", "new-value", "prod", "text/plain", DateTimeOffset.UtcNow, false, false, null);
        var mockClient = new Mock<IAzureAppConfigurationClient>();
        mockClient.Setup(c => c.GetKeyValueAsync(provider, "App:Key", "prod"))
            .ReturnsAsync(Result<AppConfigurationEntry?>.Success(existingEntry));
        mockClient.Setup(c => c.SetKeyValueAsync(provider, "App:Key", "prod", "new-value", "text/plain"))
            .ReturnsAsync(Result<AppConfigurationEntry>.Success(updatedEntry));

        var service = new AppConfigurationOperationService(store, mockClient.Object, audit);
        var command = new SetAppConfigurationValue(provider.Id, "App:Key", "prod", "new-value", null);
        var result = await service.SetKeyValueAsync(command, "test-user", Guid.Empty);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-value", result.Value!.Value);
        Assert.Single(audit.Logs);
        Assert.Equal(AuditAction.AppConfigurationKeyValueUpdated, audit.Logs[0].Action);
        Assert.Contains("old-value", audit.Logs[0].ChangeDetails);
        Assert.Contains("new-value", audit.Logs[0].ChangeDetails);
    }

    [Fact]
    public async Task SetKeyValueAsync_FailsWithoutCreateCapability_ForNewEntry()
    {
        var provider = NewAppConfigProvider(SecretProviderCapabilities.Check | SecretProviderCapabilities.Rotate);
        var store = NewStore(new[] { provider });

        var mockClient = new Mock<IAzureAppConfigurationClient>();
        mockClient.Setup(c => c.GetKeyValueAsync(provider, "App:NewKey", null))
            .ReturnsAsync(Result<AppConfigurationEntry?>.Success(null));

        var service = new AppConfigurationOperationService(store, mockClient.Object, new FakeAuditService());
        var command = new SetAppConfigurationValue(provider.Id, "App:NewKey", null, "value", null);
        var result = await service.SetKeyValueAsync(command, "user", null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Create capability", result.Error);
    }

    [Fact]
    public async Task SetKeyValueAsync_FailsWithoutRotateCapability_ForExistingEntry()
    {
        var provider = NewAppConfigProvider(SecretProviderCapabilities.Check | SecretProviderCapabilities.Create);
        var store = NewStore(new[] { provider });

        var existingEntry = new AppConfigurationEntry("App:Key", "old", null, null, null, false, false, null);
        var mockClient = new Mock<IAzureAppConfigurationClient>();
        mockClient.Setup(c => c.GetKeyValueAsync(provider, "App:Key", null))
            .ReturnsAsync(Result<AppConfigurationEntry?>.Success(existingEntry));

        var service = new AppConfigurationOperationService(store, mockClient.Object, new FakeAuditService());
        var command = new SetAppConfigurationValue(provider.Id, "App:Key", null, "new-value", null);
        var result = await service.SetKeyValueAsync(command, "user", null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Rotate capability", result.Error);
    }
}
