using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class SecretOperationServiceTests
{
    private static InMemoryFuseStore NewStore(IEnumerable<SecretProvider>? providers = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: (providers ?? Array.Empty<SecretProvider>()).ToArray(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task ListSecretsAsync_WithNonExistentProvider_ReturnsNotFound()
    {
        // Arrange
        var store = NewStore();
        var mockClient = new Mock<IAzureKeyVaultClient>();
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);

        // Act
        var result = await service.ListSecretsAsync(Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task ListSecretsAsync_WithProviderWithoutCheckCapability_ReturnsFailure()
    {
        // Arrange
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Create, // Only Create, no Check
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        var store = NewStore(providers: new[] { provider });
        var mockClient = new Mock<IAzureKeyVaultClient>();
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);

        // Act
        var result = await service.ListSecretsAsync(provider.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("does not have Check capability", result.Error);
    }

    [Fact]
    public async Task ListSecretsAsync_WithValidProvider_ReturnsSecrets()
    {
        // Arrange
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check,
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        var store = NewStore(providers: new[] { provider });
        
        var secrets = new List<SecretMetadata>
        {
            new("secret1", true, DateTimeOffset.UtcNow, null),
            new("secret2", true, DateTimeOffset.UtcNow, null)
        };
        
        var mockClient = new Mock<IAzureKeyVaultClient>();
        mockClient.Setup(c => c.ListSecretsAsync(provider))
            .ReturnsAsync(Result<IReadOnlyList<SecretMetadata>>.Success(secrets));
        
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);

        // Act
        var result = await service.ListSecretsAsync(provider.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task CreateSecretAsync_WithNonExistentProvider_ReturnsNotFound()
    {
        // Arrange
        var store = NewStore();
        var mockClient = new Mock<IAzureKeyVaultClient>();
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);
        var command = new CreateSecret(Guid.NewGuid(), "secret-name", "secret-value");

        // Act
        var result = await service.CreateSecretAsync(command, "test-user", Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task CreateSecretAsync_WithProviderWithoutCreateCapability_ReturnsFailure()
    {
        // Arrange
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check, // Only Check, no Create
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        var store = NewStore(providers: new[] { provider });
        var mockClient = new Mock<IAzureKeyVaultClient>();
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);
        var command = new CreateSecret(provider.Id, "secret-name", "secret-value");

        // Act
        var result = await service.CreateSecretAsync(command, "test-user", Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("does not have Create capability", result.Error);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateSecretAsync_WithEmptySecretName_ReturnsFailure()
    {
        // Arrange
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Create | SecretProviderCapabilities.Check,
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        var store = NewStore(providers: new[] { provider });
        var mockClient = new Mock<IAzureKeyVaultClient>();
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);
        var command = new CreateSecret(provider.Id, "", "secret-value");

        // Act
        var result = await service.CreateSecretAsync(command, "test-user", Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Secret name is required", result.Error);
    }

    [Fact]
    public async Task CreateSecretAsync_WithValidData_CreatesSecretAndAudits()
    {
        // Arrange
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Create | SecretProviderCapabilities.Check,
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        var store = NewStore(providers: new[] { provider });
        
        var mockClient = new Mock<IAzureKeyVaultClient>();
        mockClient.Setup(c => c.CreateSecretAsync(provider, "test-secret", "test-value"))
            .ReturnsAsync(Result.Success());
        
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);
        var userId = Guid.NewGuid();
        var command = new CreateSecret(provider.Id, "test-secret", "test-value");

        // Act
        var result = await service.CreateSecretAsync(command, "test-user", userId);

        // Assert
        Assert.True(result.IsSuccess);
        mockClient.Verify(c => c.CreateSecretAsync(provider, "test-secret", "test-value"), Times.Once);
        
        // Verify audit log was created
        Assert.Single(auditService.Logs);
        var auditLog = auditService.Logs[0];
        Assert.Equal(AuditAction.SecretCreated, auditLog.Action);
        Assert.Equal(AuditArea.Secret, auditLog.Area);
        Assert.Equal("test-user", auditLog.UserName);
        Assert.Equal(userId, auditLog.UserId);
        Assert.Equal(provider.Id, auditLog.EntityId);
    }

    [Fact]
    public async Task RotateSecretAsync_WithValidData_RotatesSecretAndAudits()
    {
        // Arrange
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Rotate | SecretProviderCapabilities.Check,
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        var store = NewStore(providers: new[] { provider });
        
        var mockClient = new Mock<IAzureKeyVaultClient>();
        mockClient.Setup(c => c.RotateSecretAsync(provider, "test-secret", "new-value"))
            .ReturnsAsync(Result.Success());
        
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);
        var userId = Guid.NewGuid();
        var command = new RotateSecret(provider.Id, "test-secret", "new-value");

        // Act
        var result = await service.RotateSecretAsync(command, "test-user", userId);

        // Assert
        Assert.True(result.IsSuccess);
        mockClient.Verify(c => c.RotateSecretAsync(provider, "test-secret", "new-value"), Times.Once);
        
        // Verify audit log was created
        Assert.Single(auditService.Logs);
        var auditLog = auditService.Logs[0];
        Assert.Equal(AuditAction.SecretRotated, auditLog.Action);
        Assert.Equal(AuditArea.Secret, auditLog.Area);
    }

    [Fact]
    public async Task RotateSecretAsync_WithProviderWithoutRotateCapability_ReturnsFailure()
    {
        // Arrange
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check, // No Rotate capability
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        var store = NewStore(providers: new[] { provider });
        var mockClient = new Mock<IAzureKeyVaultClient>();
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);
        var command = new RotateSecret(provider.Id, "test-secret", "new-value");

        // Act
        var result = await service.RotateSecretAsync(command, "test-user", Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("does not have Rotate capability", result.Error);
    }

    [Fact]
    public async Task RevealSecretAsync_WithValidData_RevealsSecretAndAudits()
    {
        // Arrange
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Read | SecretProviderCapabilities.Check,
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        var store = NewStore(providers: new[] { provider });
        
        var mockClient = new Mock<IAzureKeyVaultClient>();
        mockClient.Setup(c => c.ReadSecretAsync(provider, "test-secret", null))
            .ReturnsAsync(Result<string>.Success("secret-value"));
        
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);
        var userId = Guid.NewGuid();
        var command = new RevealSecret(provider.Id, "test-secret", null);

        // Act
        var result = await service.RevealSecretAsync(command, "test-user", userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("secret-value", result.Value);
        mockClient.Verify(c => c.ReadSecretAsync(provider, "test-secret", null), Times.Once);
        
        // Verify audit log was created - CRITICAL for secret reveal operations
        Assert.Single(auditService.Logs);
        var auditLog = auditService.Logs[0];
        Assert.Equal(AuditAction.SecretRevealed, auditLog.Action);
        Assert.Equal(AuditArea.Secret, auditLog.Area);
        Assert.Equal("test-user", auditLog.UserName);
        Assert.Equal(userId, auditLog.UserId);
        Assert.Contains("test-secret", auditLog.ChangeDetails);
    }

    [Fact]
    public async Task RevealSecretAsync_WithProviderWithoutReadCapability_ReturnsFailure()
    {
        // Arrange
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Check, // No Read capability
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        var store = NewStore(providers: new[] { provider });
        var mockClient = new Mock<IAzureKeyVaultClient>();
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);
        var command = new RevealSecret(provider.Id, "test-secret", null);

        // Act
        var result = await service.RevealSecretAsync(command, "test-user", Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("does not have Read capability", result.Error);
    }

    [Fact]
    public async Task RevealSecretAsync_WithSpecificVersion_PassesVersionToClient()
    {
        // Arrange
        var provider = new SecretProvider(
            Guid.NewGuid(),
            "Test Provider",
            new Uri("https://vault.azure.net"),
            SecretProviderAuthMode.ManagedIdentity,
            null,
            SecretProviderCapabilities.Read | SecretProviderCapabilities.Check,
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        var store = NewStore(providers: new[] { provider });
        
        var mockClient = new Mock<IAzureKeyVaultClient>();
        mockClient.Setup(c => c.ReadSecretAsync(provider, "test-secret", "v123"))
            .ReturnsAsync(Result<string>.Success("old-secret-value"));
        
        var auditService = new FakeAuditService();
        var service = new SecretOperationService(store, mockClient.Object, auditService);
        var command = new RevealSecret(provider.Id, "test-secret", "v123");

        // Act
        var result = await service.RevealSecretAsync(command, "test-user", Guid.NewGuid());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("old-secret-value", result.Value);
        mockClient.Verify(c => c.ReadSecretAsync(provider, "test-secret", "v123"), Times.Once);
        
        // Verify audit includes version
        var auditLog = auditService.Logs[0];
        Assert.Contains("v123", auditLog.ChangeDetails);
    }
}
