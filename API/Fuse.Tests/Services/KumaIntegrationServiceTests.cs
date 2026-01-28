using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class KumaIntegrationServiceTests
{
    private static InMemoryFuseStore NewStore(
        IEnumerable<EnvironmentInfo>? environments = null,
        IEnumerable<Platform>? platforms = null,
        IEnumerable<Account>? accounts = null,
        IEnumerable<KumaIntegration>? integrations = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: (platforms ?? Array.Empty<Platform>()).ToArray(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: (accounts ?? Array.Empty<Account>()).ToArray(),
            Identities: Array.Empty<Identity>(),
            Tags: Array.Empty<Tag>(),
            Environments: (environments ?? Array.Empty<EnvironmentInfo>()).ToArray(),
            KumaIntegrations: (integrations ?? Array.Empty<KumaIntegration>()).ToArray(),
            SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(), Positions: Array.Empty<Position>(), ResponsibilityTypes: Array.Empty<ResponsibilityType>(), ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    private static Mock<IKumaIntegrationValidator> CreateMockValidator(bool returnValue = true)
    {
        var mock = new Mock<IKumaIntegrationValidator>();
        mock.Setup(v => v.ValidateAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returnValue);
        return mock;
    }

    [Fact]
    public async Task GetKumaIntegrationsAsync_ReturnsAllIntegrations()
    {
        // Arrange
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", "Development", new HashSet<Guid>());
        var integration1 = new KumaIntegration(
            Guid.NewGuid(), "Integration 1", new[] { env1.Id }.ToList().AsReadOnly(),
            null, null, new Uri("https://kuma1.example.com"), "key1", DateTime.UtcNow, DateTime.UtcNow);
        var integration2 = new KumaIntegration(
            Guid.NewGuid(), "Integration 2", new[] { env1.Id }.ToList().AsReadOnly(),
            null, null, new Uri("https://kuma2.example.com"), "key2", DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(
            environments: new[] { env1 },
            integrations: new[] { integration1, integration2 });
        var validator = CreateMockValidator();
        var service = new KumaIntegrationService(store, validator.Object);

        // Act
        var result = await service.GetKumaIntegrationsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.Name == "Integration 1");
        Assert.Contains(result, i => i.Name == "Integration 2");
    }

    [Fact]
    public async Task GetKumaIntegrationByIdAsync_WithExistingId_ReturnsIntegration()
    {
        // Arrange
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", "Development", new HashSet<Guid>());
        var integrationId = Guid.NewGuid();
        var integration = new KumaIntegration(
            integrationId, "Test Integration", new[] { env1.Id }.ToList().AsReadOnly(),
            null, null, new Uri("https://kuma.example.com"), "key", DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(environments: new[] { env1 }, integrations: new[] { integration });
        var validator = CreateMockValidator();
        var service = new KumaIntegrationService(store, validator.Object);

        // Act
        var result = await service.GetKumaIntegrationByIdAsync(integrationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(integrationId, result.Id);
        Assert.Equal("Test Integration", result.Name);
    }

    [Fact]
    public async Task GetKumaIntegrationByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Arrange
        var store = NewStore();
        var validator = CreateMockValidator();
        var service = new KumaIntegrationService(store, validator.Object);

        // Act
        var result = await service.GetKumaIntegrationByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateKumaIntegrationAsync_WithNoEnvironments_ReturnsFailure()
    {
        // Arrange
        var store = NewStore();
        var validator = CreateMockValidator();
        var service = new KumaIntegrationService(store, validator.Object);
        var command = new CreateKumaIntegration(
            "Test", null, null, null, new Uri("https://kuma.example.com"), "key");

        // Act
        var result = await service.CreateKumaIntegrationAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("At least one environment must be specified", result.Error);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateKumaIntegrationAsync_WithNonExistentEnvironment_ReturnsFailure()
    {
        // Arrange
        var store = NewStore();
        var validator = CreateMockValidator();
        var service = new KumaIntegrationService(store, validator.Object);
        var command = new CreateKumaIntegration(
            "Test", new List<Guid> { Guid.NewGuid() }, null, null, 
            new Uri("https://kuma.example.com"), "key");

        // Act
        var result = await service.CreateKumaIntegrationAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateKumaIntegrationAsync_WithNonExistentPlatform_ReturnsFailure()
    {
        // Arrange
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", "Development", new HashSet<Guid>());
        var store = NewStore(environments: new[] { env1 });
        var validator = CreateMockValidator();
        var service = new KumaIntegrationService(store, validator.Object);
        var command = new CreateKumaIntegration(
            "Test", new List<Guid> { env1.Id }, Guid.NewGuid(), null,
            new Uri("https://kuma.example.com"), "key");

        // Act
        var result = await service.CreateKumaIntegrationAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Platform", result.Error);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task CreateKumaIntegrationAsync_WithInvalidCredentials_ReturnsFailure()
    {
        // Arrange
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", "Development", new HashSet<Guid>());
        var store = NewStore(environments: new[] { env1 });
        var validator = CreateMockValidator(returnValue: false);
        var service = new KumaIntegrationService(store, validator.Object);
        var command = new CreateKumaIntegration(
            "Test", new List<Guid> { env1.Id }, null, null,
            new Uri("https://kuma.example.com"), "invalid-key");

        // Act
        var result = await service.CreateKumaIntegrationAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to validate Kuma integration", result.Error);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateKumaIntegrationAsync_WithValidData_CreatesIntegration()
    {
        // Arrange
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", "Development", new HashSet<Guid>());
        var store = NewStore(environments: new[] { env1 });
        var validator = CreateMockValidator(returnValue: true);
        var service = new KumaIntegrationService(store, validator.Object);
        var command = new CreateKumaIntegration(
            "Test Integration", new List<Guid> { env1.Id }, null, null,
            new Uri("https://kuma.example.com"), "valid-key");

        // Act
        var result = await service.CreateKumaIntegrationAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Test Integration", result.Value.Name);
        Assert.Single(result.Value.EnvironmentIds);
        Assert.Equal(env1.Id, result.Value.EnvironmentIds[0]);
        
        var allIntegrations = await service.GetKumaIntegrationsAsync();
        Assert.Single(allIntegrations);
    }

    [Fact]
    public async Task CreateKumaIntegrationAsync_WithoutName_UsesHostAsName()
    {
        // Arrange
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", "Development", new HashSet<Guid>());
        var store = NewStore(environments: new[] { env1 });
        var validator = CreateMockValidator(returnValue: true);
        var service = new KumaIntegrationService(store, validator.Object);
        var uri = new Uri("https://kuma.example.com");
        var command = new CreateKumaIntegration(
            null, new List<Guid> { env1.Id }, null, null, uri, "valid-key");

        // Act
        var result = await service.CreateKumaIntegrationAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(uri.Host, result.Value.Name);
    }

    [Fact]
    public async Task UpdateKumaIntegrationAsync_WithNonExistentIntegration_ReturnsFailure()
    {
        // Arrange
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", "Development", new HashSet<Guid>());
        var store = NewStore(environments: new[] { env1 });
        var validator = CreateMockValidator();
        var service = new KumaIntegrationService(store, validator.Object);
        var command = new UpdateKumaIntegration(
            Guid.NewGuid(), "Updated", new List<Guid> { env1.Id }, null, null,
            new Uri("https://kuma.example.com"), "key");

        // Act
        var result = await service.UpdateKumaIntegrationAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateKumaIntegrationAsync_WithChangedCredentials_ValidatesConnection()
    {
        // Arrange
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", "Development", new HashSet<Guid>());
        var integrationId = Guid.NewGuid();
        var integration = new KumaIntegration(
            integrationId, "Test", new[] { env1.Id }.ToList().AsReadOnly(),
            null, null, new Uri("https://kuma.example.com"), "old-key", DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(environments: new[] { env1 }, integrations: new[] { integration });
        var validator = CreateMockValidator(returnValue: false);
        var service = new KumaIntegrationService(store, validator.Object);
        var command = new UpdateKumaIntegration(
            integrationId, "Updated", new List<Guid> { env1.Id }, null, null,
            new Uri("https://kuma.example.com"), "new-invalid-key");

        // Act
        var result = await service.UpdateKumaIntegrationAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to validate", result.Error);
        validator.Verify(v => v.ValidateAsync(It.IsAny<Uri>(), "new-invalid-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateKumaIntegrationAsync_WithUnchangedCredentials_SkipsValidation()
    {
        // Arrange
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", "Development", new HashSet<Guid>());
        var integrationId = Guid.NewGuid();
        var uri = new Uri("https://kuma.example.com");
        var integration = new KumaIntegration(
            integrationId, "Test", new[] { env1.Id }.ToList().AsReadOnly(),
            null, null, uri, "same-key", DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(environments: new[] { env1 }, integrations: new[] { integration });
        var validator = CreateMockValidator();
        var service = new KumaIntegrationService(store, validator.Object);
        var command = new UpdateKumaIntegration(
            integrationId, "Updated Name", new List<Guid> { env1.Id }, null, null,
            uri, "same-key");

        // Act
        var result = await service.UpdateKumaIntegrationAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Name", result.Value!.Name);
        validator.Verify(v => v.ValidateAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateKumaIntegrationAsync_WithValidData_UpdatesIntegration()
    {
        // Arrange
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", "Development", new HashSet<Guid>());
        var env2 = new EnvironmentInfo(Guid.NewGuid(), "prod", "Production", new HashSet<Guid>());
        var integrationId = Guid.NewGuid();
        var integration = new KumaIntegration(
            integrationId, "Test", new[] { env1.Id }.ToList().AsReadOnly(),
            null, null, new Uri("https://kuma.example.com"), "key", DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(environments: new[] { env1, env2 }, integrations: new[] { integration });
        var validator = CreateMockValidator(returnValue: true);
        var service = new KumaIntegrationService(store, validator.Object);
        var command = new UpdateKumaIntegration(
            integrationId, "Updated Integration", new List<Guid> { env1.Id, env2.Id }, null, null,
            new Uri("https://kuma-new.example.com"), "new-key");

        // Act
        var result = await service.UpdateKumaIntegrationAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Updated Integration", result.Value.Name);
        Assert.Equal(2, result.Value.EnvironmentIds.Count);
        Assert.Equal("https://kuma-new.example.com/", result.Value.Uri.ToString());
    }

    [Fact]
    public async Task DeleteKumaIntegrationAsync_WithNonExistentIntegration_ReturnsFailure()
    {
        // Arrange
        var store = NewStore();
        var validator = CreateMockValidator();
        var service = new KumaIntegrationService(store, validator.Object);
        var command = new DeleteKumaIntegration(Guid.NewGuid());

        // Act
        var result = await service.DeleteKumaIntegrationAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteKumaIntegrationAsync_WithExistingIntegration_DeletesIntegration()
    {
        // Arrange
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "dev", "Development", new HashSet<Guid>());
        var integrationId = Guid.NewGuid();
        var integration = new KumaIntegration(
            integrationId, "Test", new[] { env1.Id }.ToList().AsReadOnly(),
            null, null, new Uri("https://kuma.example.com"), "key", DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(environments: new[] { env1 }, integrations: new[] { integration });
        var validator = CreateMockValidator();
        var service = new KumaIntegrationService(store, validator.Object);
        var command = new DeleteKumaIntegration(integrationId);

        // Act
        var result = await service.DeleteKumaIntegrationAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        
        var allIntegrations = await service.GetKumaIntegrationsAsync();
        Assert.Empty(allIntegrations);
    }
}
