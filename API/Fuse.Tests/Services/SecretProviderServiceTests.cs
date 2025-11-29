using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class SecretProviderServiceTests
{
    private static InMemoryFuseStore NewStore(
        IEnumerable<SecretProvider>? providers = null,
        IEnumerable<Account>? accounts = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: (accounts ?? Array.Empty<Account>()).ToArray(),
            Identities: Array.Empty<Identity>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: (providers ?? Array.Empty<SecretProvider>()).ToArray(),
            SqlIntegrations: Array.Empty<SqlIntegration>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    private static Mock<IAzureKeyVaultClient> CreateMockClient(bool testConnectionSuccess = true)
    {
        var mock = new Mock<IAzureKeyVaultClient>();
        mock.Setup(c => c.TestConnectionAsync(It.IsAny<Uri>(), It.IsAny<SecretProviderAuthMode>(), It.IsAny<SecretProviderCredentials?>()))
            .ReturnsAsync(testConnectionSuccess ? Result.Success() : Result.Failure("Connection failed"));
        return mock;
    }

    [Fact]
    public async Task GetSecretProvidersAsync_ReturnsAllProviders()
    {
        // Arrange
        var provider1 = new SecretProvider(
            Guid.NewGuid(), "Provider 1", new Uri("https://vault1.azure.net"),
            SecretProviderAuthMode.ManagedIdentity, null,
            SecretProviderCapabilities.Check, DateTime.UtcNow, DateTime.UtcNow);
        var provider2 = new SecretProvider(
            Guid.NewGuid(), "Provider 2", new Uri("https://vault2.azure.net"),
            SecretProviderAuthMode.ManagedIdentity, null,
            SecretProviderCapabilities.Check, DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(providers: new[] { provider1, provider2 });
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);

        // Act
        var result = await service.GetSecretProvidersAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "Provider 1");
        Assert.Contains(result, p => p.Name == "Provider 2");
    }

    [Fact]
    public async Task GetSecretProviderByIdAsync_WithExistingId_ReturnsProvider()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new SecretProvider(
            providerId, "Test Provider", new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity, null,
            SecretProviderCapabilities.Check, DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(providers: new[] { provider });
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);

        // Act
        var result = await service.GetSecretProviderByIdAsync(providerId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(providerId, result.Id);
        Assert.Equal("Test Provider", result.Name);
    }

    [Fact]
    public async Task GetSecretProviderByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);

        // Act
        var result = await service.GetSecretProviderByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateSecretProviderAsync_WithEmptyName_ReturnsFailure()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new CreateSecretProvider(
            "",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check
        );

        // Act
        var result = await service.CreateSecretProviderAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Name is required", result.Error);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateSecretProviderAsync_WithoutCheckCapability_ReturnsFailure()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new CreateSecretProvider(
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Create // Missing Check capability
        );

        // Act
        var result = await service.CreateSecretProviderAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Check capability is required", result.Error);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateSecretProviderAsync_WithClientSecretButNoCredentials_ReturnsFailure()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new CreateSecretProvider(
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ClientSecret,
            null, // Missing credentials
            SecretProviderCapabilities.Check
        );

        // Act
        var result = await service.CreateSecretProviderAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Client secret authentication requires", result.Error);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateSecretProviderAsync_WithClientSecretButIncompleteCredentials_ReturnsFailure()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);
        var incompleteCredentials = new SecretProviderCredentials("tenant-id", null, null);
        var command = new CreateSecretProvider(
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ClientSecret,
            incompleteCredentials,
            SecretProviderCapabilities.Check
        );

        // Act
        var result = await service.CreateSecretProviderAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Client secret authentication requires", result.Error);
    }

    [Fact]
    public async Task CreateSecretProviderAsync_WithFailedConnectionTest_ReturnsFailure()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient(testConnectionSuccess: false);
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new CreateSecretProvider(
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check
        );

        // Act
        var result = await service.CreateSecretProviderAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Connection test failed", result.Error);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateSecretProviderAsync_WithValidData_CreatesProvider()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient(testConnectionSuccess: true);
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new CreateSecretProvider(
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check | SecretProviderCapabilities.Read
        );

        // Act
        var result = await service.CreateSecretProviderAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Test Provider", result.Value.Name);
        Assert.Equal(SecretProviderAuthMode.ManagedIdentity, result.Value.AuthMode);
        Assert.True(result.Value.Capabilities.HasFlag(SecretProviderCapabilities.Check));
        Assert.True(result.Value.Capabilities.HasFlag(SecretProviderCapabilities.Read));
        
        mockClient.Verify(c => c.TestConnectionAsync(
            It.Is<Uri>(u => u.ToString() == "https://vault.azure.net/"),
            SecretProviderAuthMode.ManagedIdentity,
            null), Times.Once);
        
        var allProviders = await service.GetSecretProvidersAsync();
        Assert.Single(allProviders);
    }

    [Fact]
    public async Task UpdateSecretProviderAsync_WithNonExistentProvider_ReturnsNotFound()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new UpdateSecretProvider(
            Guid.NewGuid(),
            "Updated Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check
        );

        // Act
        var result = await service.UpdateSecretProviderAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateSecretProviderAsync_WithUnchangedCredentials_SkipsConnectionTest()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var uri = new Uri("https://vault.azure.net");
        var provider = new SecretProvider(
            providerId, "Test Provider", uri,
            SecretProviderAuthMode.ManagedIdentity, null,
            SecretProviderCapabilities.Check, DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(providers: new[] { provider });
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new UpdateSecretProvider(
            providerId,
            "Updated Name",
            uri,
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check | SecretProviderCapabilities.Read
        );

        // Act
        var result = await service.UpdateSecretProviderAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Name", result.Value!.Name);
        
        // Connection test should not have been called since credentials didn't change
        mockClient.Verify(c => c.TestConnectionAsync(
            It.IsAny<Uri>(),
            It.IsAny<SecretProviderAuthMode>(),
            It.IsAny<SecretProviderCredentials?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSecretProviderAsync_WithChangedUri_TestsConnection()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new SecretProvider(
            providerId, "Test Provider", new Uri("https://vault-old.azure.net"),
            SecretProviderAuthMode.ManagedIdentity, null,
            SecretProviderCapabilities.Check, DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(providers: new[] { provider });
        var mockClient = CreateMockClient(testConnectionSuccess: true);
        var service = new SecretProviderService(store, mockClient.Object);
        var newUri = new Uri("https://vault-new.azure.net");
        var command = new UpdateSecretProvider(
            providerId,
            "Test Provider",
            newUri,
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check
        );

        // Act
        var result = await service.UpdateSecretProviderAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(newUri, result.Value!.VaultUri);
        
        // Connection test should have been called
        mockClient.Verify(c => c.TestConnectionAsync(
            newUri,
            SecretProviderAuthMode.ManagedIdentity,
            null), Times.Once);
    }

    [Fact]
    public async Task UpdateSecretProviderAsync_WithChangedCredentials_TestsConnection()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var oldCredentials = new SecretProviderCredentials("tenant1", "client1", "secret1");
        var provider = new SecretProvider(
            providerId, "Test Provider", new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ClientSecret, oldCredentials,
            SecretProviderCapabilities.Check, DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(providers: new[] { provider });
        var mockClient = CreateMockClient(testConnectionSuccess: true);
        var service = new SecretProviderService(store, mockClient.Object);
        var newCredentials = new SecretProviderCredentials("tenant1", "client1", "secret2");
        var command = new UpdateSecretProvider(
            providerId,
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ClientSecret,
            newCredentials,
            SecretProviderCapabilities.Check
        );

        // Act
        var result = await service.UpdateSecretProviderAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Connection test should have been called due to changed credentials
        mockClient.Verify(c => c.TestConnectionAsync(
            It.IsAny<Uri>(),
            SecretProviderAuthMode.ClientSecret,
            newCredentials), Times.Once);
    }

    [Fact]
    public async Task DeleteSecretProviderAsync_WithNonExistentProvider_ReturnsNotFound()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new DeleteSecretProvider(Guid.NewGuid());

        // Act
        var result = await service.DeleteSecretProviderAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteSecretProviderAsync_WithProviderInUse_ReturnsFailure()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new SecretProvider(
            providerId, "Test Provider", new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity, null,
            SecretProviderCapabilities.Check, DateTime.UtcNow, DateTime.UtcNow);

        var account = new Account(
            Guid.NewGuid(),
            Guid.NewGuid(), // TargetId
            TargetKind.Application,
            AuthKind.UserPassword,
            new SecretBinding(
                SecretBindingKind.AzureKeyVault,
                null,
                new AzureKeyVaultBinding(providerId, "secret-name", null)
            ),
            "username",
            null,
            Array.Empty<Grant>(),
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var store = NewStore(providers: new[] { provider }, accounts: new[] { account });
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new DeleteSecretProvider(providerId);

        // Act
        var result = await service.DeleteSecretProviderAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Cannot delete secret provider", result.Error);
        Assert.Contains("account(s) are using it", result.Error);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task DeleteSecretProviderAsync_WithUnusedProvider_DeletesProvider()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new SecretProvider(
            providerId, "Test Provider", new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity, null,
            SecretProviderCapabilities.Check, DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(providers: new[] { provider });
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new DeleteSecretProvider(providerId);

        // Act
        var result = await service.DeleteSecretProviderAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        
        var allProviders = await service.GetSecretProvidersAsync();
        Assert.Empty(allProviders);
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient(testConnectionSuccess: true);
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new TestSecretProviderConnection(
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null
        );

        // Act
        var result = await service.TestConnectionAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        mockClient.Verify(c => c.TestConnectionAsync(
            It.Is<Uri>(u => u.ToString() == "https://vault.azure.net/"),
            SecretProviderAuthMode.ManagedIdentity,
            null), Times.Once);
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidCredentials_ReturnsFailure()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient(testConnectionSuccess: false);
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new TestSecretProviderConnection(
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null
        );

        // Act
        var result = await service.TestConnectionAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Connection failed", result.Error);
    }

    [Fact]
    public async Task TestConnectionAsync_WithClientSecretButNoCredentials_ReturnsFailure()
    {
        // Arrange
        var store = NewStore();
        var mockClient = CreateMockClient();
        var service = new SecretProviderService(store, mockClient.Object);
        var command = new TestSecretProviderConnection(
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ClientSecret,
            null
        );

        // Act
        var result = await service.TestConnectionAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Client secret authentication requires", result.Error);
    }
}
