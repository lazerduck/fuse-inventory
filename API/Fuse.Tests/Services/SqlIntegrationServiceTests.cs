using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class SqlIntegrationServiceTests
{
    private static InMemoryFuseStore NewStore(IEnumerable<SqlIntegration>? integrations = null, IEnumerable<DataStore>? dataStores = null, IEnumerable<Account>? accounts = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: (dataStores ?? Array.Empty<DataStore>()).ToArray(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: (accounts ?? Array.Empty<Account>()).ToArray(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: (integrations ?? Array.Empty<SqlIntegration>()).ToArray(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    private static SqlIntegrationService CreateService(
        InMemoryFuseStore store,
        ISqlConnectionValidator? validator = null,
        IAccountSqlInspector? inspector = null,
        IAuditService? auditService = null)
    {
        return new SqlIntegrationService(
            store,
            validator ?? Mock.Of<ISqlConnectionValidator>(),
            inspector ?? Mock.Of<IAccountSqlInspector>(),
            auditService ?? Mock.Of<IAuditService>());
    }

    [Fact]
    public async Task GetSqlIntegrationsAsync_ReturnsAll()
    {
        var ds1 = Guid.NewGuid();
        var int1 = new SqlIntegration(Guid.NewGuid(), "SQL1", ds1, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        var int2 = new SqlIntegration(Guid.NewGuid(), "SQL2", Guid.NewGuid(), "Server=test2;", SqlPermissions.Read | SqlPermissions.Write, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { int1, int2 });
        var service = CreateService(store);

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
        var service = CreateService(store);

        var result = await service.GetSqlIntegrationByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal(id, result.Id);
        Assert.Equal("SQL1", result.Name);
    }

    [Fact]
    public async Task CreateSqlIntegrationAsync_ValidatesName()
    {
        var store = NewStore();
        var service = CreateService(store);

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
        var service = CreateService(store);

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
        var service = CreateService(store);

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
        var service = CreateService(store);

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
        var service = CreateService(store, validator: mockValidator.Object);

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
        var service = CreateService(store, validator: mockValidator.Object);

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
        var service = CreateService(store);

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
        var service = CreateService(store, validator: mockValidator.Object);

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
        var service = CreateService(store, validator: mockValidator.Object);

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
        var service = CreateService(store);

        var result = await service.DeleteSqlIntegrationAsync(new DeleteSqlIntegration(id));

        Assert.True(result.IsSuccess);
        var remaining = await service.GetSqlIntegrationsAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task TestConnectionAsync_ValidatesConnectionString()
    {
        var store = NewStore();
        var service = CreateService(store);

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
        var service = CreateService(store, validator: mockValidator.Object);

        var command = new TestSqlConnection("Server=test;");
        var result = await service.TestConnectionAsync(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.IsSuccessful);
        Assert.Equal(SqlPermissions.Read | SqlPermissions.Write, result.Value.Permissions);
    }

    [Fact]
    public async Task GetPermissionsOverviewAsync_ReturnsNotFoundForMissingIntegration()
    {
        var store = NewStore();
        var service = CreateService(store);

        var result = await service.GetPermissionsOverviewAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetPermissionsOverviewAsync_ReturnsErrorWhenNoReadPermission()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.None, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds });
        var service = CreateService(store);

        var result = await service.GetPermissionsOverviewAsync(intId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Contains("does not have Read permission", result.Value.ErrorMessage);
    }

    [Fact]
    public async Task GetPermissionsOverviewAsync_ReturnsEmptySummaryWhenNoAccounts()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds });
        var service = CreateService(store);

        var result = await service.GetPermissionsOverviewAsync(intId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(intId, result.Value.IntegrationId);
        Assert.Equal("SQL1", result.Value.IntegrationName);
        Assert.Empty(result.Value.Accounts);
        Assert.Equal(0, result.Value.Summary.TotalAccounts);
    }

    [Fact]
    public async Task GetPermissionsOverviewAsync_ReturnsAccountStatuses()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
            "testuser",
            null,
            new List<Grant>
            {
                new Grant(Guid.NewGuid(), "TestDB", null, new HashSet<Privilege> { Privilege.Select })
            },
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, new List<SqlActualGrant>
            {
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select })
            }), null));
        
        var service = CreateService(store, inspector: mockInspector.Object);

        var result = await service.GetPermissionsOverviewAsync(intId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.Accounts);
        Assert.Equal(accountId, result.Value.Accounts[0].AccountId);
        Assert.Equal("testuser", result.Value.Accounts[0].PrincipalName);
        Assert.Equal(Fuse.Core.Responses.SyncStatus.InSync, result.Value.Accounts[0].Status);
        Assert.Equal(1, result.Value.Summary.TotalAccounts);
        Assert.Equal(1, result.Value.Summary.InSyncCount);
    }

    [Fact]
    public async Task GetPermissionsOverviewAsync_DetectsDrift()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
            "testuser",
            null,
            new List<Grant>
            {
                new Grant(Guid.NewGuid(), "TestDB", null, new HashSet<Privilege> { Privilege.Select, Privilege.Insert })
            },
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, new List<SqlActualGrant>
            {
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select })
            }), null));
        
        var service = CreateService(store, inspector: mockInspector.Object);

        var result = await service.GetPermissionsOverviewAsync(intId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.Accounts);
        Assert.Equal(Fuse.Core.Responses.SyncStatus.DriftDetected, result.Value.Accounts[0].Status);
        Assert.Equal(1, result.Value.Summary.DriftCount);
    }

    [Fact]
    public async Task GetPermissionsOverviewAsync_DetectsMissingPrincipal()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
            "testuser",
            null,
            new List<Grant>
            {
                new Grant(Guid.NewGuid(), "TestDB", null, new HashSet<Privilege> { Privilege.Select })
            },
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", false, Array.Empty<SqlActualGrant>()), null));
        
        var service = CreateService(store, inspector: mockInspector.Object);

        var result = await service.GetPermissionsOverviewAsync(intId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.Accounts);
        Assert.Equal(Fuse.Core.Responses.SyncStatus.MissingPrincipal, result.Value.Accounts[0].Status);
        Assert.Equal(1, result.Value.Summary.MissingPrincipalCount);
    }

    [Fact]
    public async Task ResolveDriftAsync_ReturnsNotFoundForMissingIntegration()
    {
        var store = NewStore();
        var service = CreateService(store);

        var result = await service.ResolveDriftAsync(new ResolveDrift(Guid.NewGuid(), Guid.NewGuid()), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Contains("not found", result.Error!);
    }

    [Fact]
    public async Task ResolveDriftAsync_ReturnsErrorWhenNoWritePermission()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow); // No Write permission
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
            "testuser",
            null,
            new List<Grant> { new Grant(Guid.NewGuid(), "TestDB", null, new HashSet<Privilege> { Privilege.Select }) },
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        var service = CreateService(store);

        var result = await service.ResolveDriftAsync(new ResolveDrift(intId, accountId), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Write permission", result.Error!);
    }

    [Fact]
    public async Task ResolveDriftAsync_ReturnsNotFoundForMissingAccount()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds });
        var service = CreateService(store);

        var result = await service.ResolveDriftAsync(new ResolveDrift(intId, Guid.NewGuid()), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Contains("Account", result.Error!);
    }

    [Fact]
    public async Task ResolveDriftAsync_ReturnsErrorWhenAccountNotAssociatedWithDataStore()
    {
        var dsId = Guid.NewGuid();
        var otherDsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var otherDs = new DataStore(otherDsId, "DS2", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write, DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            accountId,
            otherDsId, // Different datastore
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
            "testuser",
            null,
            new List<Grant>(),
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds, otherDs }, accounts: new[] { account });
        var service = CreateService(store);

        var result = await service.ResolveDriftAsync(new ResolveDrift(intId, accountId), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("not associated", result.Error!);
    }

    [Fact]
    public async Task ResolveDriftAsync_ReturnsErrorWhenAccountHasNoUsername()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write, DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
            null, // No username
            null,
            new List<Grant>(),
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        var service = CreateService(store);

        var result = await service.ResolveDriftAsync(new ResolveDrift(intId, accountId), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("username", result.Error!.ToLowerInvariant());
    }

    [Fact]
    public async Task ResolveDriftAsync_ReturnsErrorWhenPrincipalDoesNotExist()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write, DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
            "testuser",
            null,
            new List<Grant> { new Grant(Guid.NewGuid(), "TestDB", null, new HashSet<Privilege> { Privilege.Select }) },
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", false, Array.Empty<SqlActualGrant>()), null)); // Principal doesn't exist
        
        var service = CreateService(store, inspector: mockInspector.Object);

        var result = await service.ResolveDriftAsync(new ResolveDrift(intId, accountId), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("does not exist", result.Error!);
    }

    [Fact]
    public async Task ResolveDriftAsync_ReturnsSuccessWhenAlreadyInSync()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write, DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
            "testuser",
            null,
            new List<Grant> { new Grant(Guid.NewGuid(), "TestDB", null, new HashSet<Privilege> { Privilege.Select }) },
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, new List<SqlActualGrant>
            {
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select })
            }), null)); // Already in sync
        
        var service = CreateService(store, inspector: mockInspector.Object);

        var result = await service.ResolveDriftAsync(new ResolveDrift(intId, accountId), "testUser", null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Success);
        Assert.Empty(result.Value.Operations);
        Assert.Equal(SyncStatus.InSync, result.Value.UpdatedStatus.Status);
    }

    [Fact]
    public async Task ResolveDriftAsync_AppliesGrantsAndRevokes()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write, DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
            "testuser",
            null,
            new List<Grant> { new Grant(Guid.NewGuid(), "TestDB", null, new HashSet<Privilege> { Privilege.Select, Privilege.Insert }) },
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        
        // Initial state: has SELECT and DELETE, but needs INSERT (has extra DELETE)
        mockInspector
            .SetupSequence(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, new List<SqlActualGrant>
            {
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select, Privilege.Delete })
            }), null))
            // After applying changes, now in sync
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, new List<SqlActualGrant>
            {
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select, Privilege.Insert })
            }), null));

        mockInspector
            .Setup(i => i.ApplyPermissionChangesAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<IReadOnlyList<SqlPermissionComparison>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new List<DriftResolutionOperation>
            {
                new DriftResolutionOperation("GRANT", "TestDB", null, Privilege.Insert, true, null),
                new DriftResolutionOperation("REVOKE", "TestDB", null, Privilege.Delete, true, null)
            }, null));
        
        var mockAuditService = new Mock<IAuditService>();
        var service = CreateService(store, inspector: mockInspector.Object, auditService: mockAuditService.Object);

        var result = await service.ResolveDriftAsync(new ResolveDrift(intId, accountId), "testUser", Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Success);
        Assert.Equal(2, result.Value.Operations.Count);
        Assert.Contains(result.Value.Operations, o => o.OperationType == "GRANT" && o.Privilege == Privilege.Insert);
        Assert.Contains(result.Value.Operations, o => o.OperationType == "REVOKE" && o.Privilege == Privilege.Delete);
        Assert.Equal(SyncStatus.InSync, result.Value.UpdatedStatus.Status);
        
        // Verify audit log was created
        mockAuditService.Verify(a => a.LogAsync(It.Is<AuditLog>(l => l.Action == AuditAction.SqlIntegrationDriftResolved), It.IsAny<CancellationToken>()), Times.Once);
    }
}
