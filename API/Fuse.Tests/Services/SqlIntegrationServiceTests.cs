using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class SqlIntegrationServiceTests
{
    private static InMemoryFuseStore NewStore(IEnumerable<SqlIntegration>? integrations = null, IEnumerable<DataStore>? dataStores = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: (dataStores ?? Array.Empty<DataStore>()).ToArray(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: (integrations ?? Array.Empty<SqlIntegration>()).ToArray(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task GetSqlIntegrationsAsync_ReturnsAll()
    {
        var ds1 = Guid.NewGuid();
        var int1 = new SqlIntegration(Guid.NewGuid(), "SQL1", ds1, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        var int2 = new SqlIntegration(Guid.NewGuid(), "SQL2", Guid.NewGuid(), "Server=test2;", SqlPermissions.Read | SqlPermissions.Write, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { int1, int2 });
        var validator = Mock.Of<ISqlConnectionValidator>();
        var service = new SqlIntegrationService(store, validator);

        var result = await service.GetSqlIntegrationsAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Id == int1.Id && r.Name == "SQL1");
        Assert.Contains(result, r => r.Id == int2.Id && r.Name == "SQL2");
    }

    [Fact]
    public async Task GetSqlIntegrationByIdAsync_ReturnsCorrectIntegration()
    {
        var id = Guid.NewGuid();
        var integration = new SqlIntegration(id, "SQL1", Guid.NewGuid(), "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration });
        var validator = Mock.Of<ISqlConnectionValidator>();
        var service = new SqlIntegrationService(store, validator);

        var result = await service.GetSqlIntegrationByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal(id, result.Id);
        Assert.Equal("SQL1", result.Name);
    }

    [Fact]
    public async Task CreateSqlIntegrationAsync_ValidatesName()
    {
        var store = NewStore();
        var validator = Mock.Of<ISqlConnectionValidator>();
        var service = new SqlIntegrationService(store, validator);

        var command = new CreateSqlIntegration("", Guid.NewGuid(), "Server=test;");
        var result = await service.CreateSqlIntegrationAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Name is required", result.Error!);
    }

    [Fact]
    public async Task CreateSqlIntegrationAsync_ValidatesConnectionString()
    {
        var store = NewStore();
        var validator = Mock.Of<ISqlConnectionValidator>();
        var service = new SqlIntegrationService(store, validator);

        var command = new CreateSqlIntegration("SQL1", Guid.NewGuid(), "");
        var result = await service.CreateSqlIntegrationAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Connection string is required", result.Error!);
    }

    [Fact]
    public async Task CreateSqlIntegrationAsync_ValidatesDataStoreExists()
    {
        var store = NewStore();
        var validator = Mock.Of<ISqlConnectionValidator>();
        var service = new SqlIntegrationService(store, validator);

        var command = new CreateSqlIntegration("SQL1", Guid.NewGuid(), "Server=test;");
        var result = await service.CreateSqlIntegrationAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Contains("DataStore", result.Error!);
    }

    [Fact]
    public async Task CreateSqlIntegrationAsync_PreventsMultipleIntegrationsPerDataStore()
    {
        var dsId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var existing = new SqlIntegration(Guid.NewGuid(), "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { existing }, dataStores: new[] { ds });
        var validator = Mock.Of<ISqlConnectionValidator>();
        var service = new SqlIntegrationService(store, validator);

        var command = new CreateSqlIntegration("SQL2", dsId, "Server=test2;");
        var result = await service.CreateSqlIntegrationAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Contains("already has an SQL integration", result.Error!);
    }

    [Fact]
    public async Task CreateSqlIntegrationAsync_TestsConnection()
    {
        var dsId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(dataStores: new[] { ds });
        var mockValidator = new Mock<ISqlConnectionValidator>();
        mockValidator
            .Setup(v => v.ValidateConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, SqlPermissions.None, "Connection failed"));
        var service = new SqlIntegrationService(store, mockValidator.Object);

        var command = new CreateSqlIntegration("SQL1", dsId, "Server=test;");
        var result = await service.CreateSqlIntegrationAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Connection validation failed", result.Error!);
    }

    [Fact]
    public async Task CreateSqlIntegrationAsync_CreatesIntegrationSuccessfully()
    {
        var dsId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(dataStores: new[] { ds });
        var mockValidator = new Mock<ISqlConnectionValidator>();
        mockValidator
            .Setup(v => v.ValidateConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, SqlPermissions.Read | SqlPermissions.Write, null));
        var service = new SqlIntegrationService(store, mockValidator.Object);

        var command = new CreateSqlIntegration("SQL1", dsId, "Server=test;");
        var result = await service.CreateSqlIntegrationAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("SQL1", result.Value.Name);
        Assert.Equal(dsId, result.Value.DataStoreId);
        Assert.Equal(SqlPermissions.Read | SqlPermissions.Write, result.Value.Permissions);
    }

    [Fact]
    public async Task UpdateSqlIntegrationAsync_ValidatesIntegrationExists()
    {
        var store = NewStore();
        var validator = Mock.Of<ISqlConnectionValidator>();
        var service = new SqlIntegrationService(store, validator);

        var command = new UpdateSqlIntegration(Guid.NewGuid(), "SQL1", Guid.NewGuid(), "Server=test;");
        var result = await service.UpdateSqlIntegrationAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateSqlIntegrationAsync_RevalidatesConnectionIfChanged()
    {
        var dsId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var id = Guid.NewGuid();
        var integration = new SqlIntegration(id, "SQL1", dsId, "Server=old;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds });
        var mockValidator = new Mock<ISqlConnectionValidator>();
        mockValidator
            .Setup(v => v.ValidateConnectionAsync("Server=new;", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, null));
        var service = new SqlIntegrationService(store, mockValidator.Object);

        var command = new UpdateSqlIntegration(id, "SQL1-Updated", dsId, "Server=new;");
        var result = await service.UpdateSqlIntegrationAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("SQL1-Updated", result.Value.Name);
        Assert.Equal(SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, result.Value.Permissions);
        mockValidator.Verify(v => v.ValidateConnectionAsync("Server=new;", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSqlIntegrationAsync_SkipsValidationIfConnectionUnchanged()
    {
        var dsId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var id = Guid.NewGuid();
        var integration = new SqlIntegration(id, "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds });
        var mockValidator = new Mock<ISqlConnectionValidator>();
        var service = new SqlIntegrationService(store, mockValidator.Object);

        var command = new UpdateSqlIntegration(id, "SQL1-Updated", dsId, "Server=test;");
        var result = await service.UpdateSqlIntegrationAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("SQL1-Updated", result.Value.Name);
        mockValidator.Verify(v => v.ValidateConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteSqlIntegrationAsync_DeletesSuccessfully()
    {
        var id = Guid.NewGuid();
        var integration = new SqlIntegration(id, "SQL1", Guid.NewGuid(), "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration });
        var validator = Mock.Of<ISqlConnectionValidator>();
        var service = new SqlIntegrationService(store, validator);

        var result = await service.DeleteSqlIntegrationAsync(new DeleteSqlIntegration(id));

        Assert.True(result.IsSuccess);
        var remaining = await service.GetSqlIntegrationsAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task TestConnectionAsync_ValidatesConnectionString()
    {
        var store = NewStore();
        var validator = Mock.Of<ISqlConnectionValidator>();
        var service = new SqlIntegrationService(store, validator);

        var command = new TestSqlConnection("");
        var result = await service.TestConnectionAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Connection string is required", result.Error!);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsTestResult()
    {
        var store = NewStore();
        var mockValidator = new Mock<ISqlConnectionValidator>();
        mockValidator
            .Setup(v => v.ValidateConnectionAsync("Server=test;", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, SqlPermissions.Read | SqlPermissions.Write, null));
        var service = new SqlIntegrationService(store, mockValidator.Object);

        var command = new TestSqlConnection("Server=test;");
        var result = await service.TestConnectionAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.IsSuccessful);
        Assert.Equal(SqlPermissions.Read | SqlPermissions.Write, result.Value.Permissions);
    }
}
