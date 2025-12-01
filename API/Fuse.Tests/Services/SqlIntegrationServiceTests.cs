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
            Identities: Array.Empty<Identity>(),
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
        IAuditService? auditService = null,
        ISecretOperationService? secretOperationService = null)
    {
        return new SqlIntegrationService(
            store,
            validator ?? Mock.Of<ISqlConnectionValidator>(),
            inspector ?? Mock.Of<IAccountSqlInspector>(),
            auditService ?? Mock.Of<IAuditService>(),
            secretOperationService ?? Mock.Of<ISecretOperationService>());
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

    [Fact]
    public async Task CreateSqlAccountAsync_ReturnsNotFoundForMissingIntegration()
    {
        var store = NewStore();
        var service = CreateService(store);

        var command = new CreateSqlAccount(Guid.NewGuid(), Guid.NewGuid(), PasswordSource.Manual, "testpass");
        var result = await service.CreateSqlAccountAsync(command, "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Contains("not found", result.Error!);
    }

    [Fact]
    public async Task CreateSqlAccountAsync_ReturnsErrorWhenNoCreatePermission()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        // Integration only has Read|Write, not Create
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
        var service = CreateService(store);

        var command = new CreateSqlAccount(intId, accountId, PasswordSource.Manual, "testpass");
        var result = await service.CreateSqlAccountAsync(command, "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Create permission", result.Error!);
    }

    [Fact]
    public async Task CreateSqlAccountAsync_ReturnsNotFoundForMissingAccount()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds });
        var service = CreateService(store);

        var command = new CreateSqlAccount(intId, Guid.NewGuid(), PasswordSource.Manual, "testpass");
        var result = await service.CreateSqlAccountAsync(command, "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Contains("Account", result.Error!);
    }

    [Fact]
    public async Task CreateSqlAccountAsync_ReturnsErrorWhenAccountNotAssociatedWithDataStore()
    {
        var dsId = Guid.NewGuid();
        var otherDsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var otherDs = new DataStore(otherDsId, "DS2", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
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

        var command = new CreateSqlAccount(intId, accountId, PasswordSource.Manual, "testpass");
        var result = await service.CreateSqlAccountAsync(command, "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("not associated", result.Error!);
    }

    [Fact]
    public async Task CreateSqlAccountAsync_ReturnsErrorWhenAccountHasNoUsername()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
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

        var command = new CreateSqlAccount(intId, accountId, PasswordSource.Manual, "testpass");
        var result = await service.CreateSqlAccountAsync(command, "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("username", result.Error!.ToLowerInvariant());
    }

    [Fact]
    public async Task CreateSqlAccountAsync_ReturnsConflictWhenPrincipalAlreadyExists()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
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
            }), null)); // Principal already exists
        
        var service = CreateService(store, inspector: mockInspector.Object);

        var command = new CreateSqlAccount(intId, accountId, PasswordSource.Manual, "testpass");
        var result = await service.CreateSqlAccountAsync(command, "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Contains("already exists", result.Error!);
    }

    [Fact]
    public async Task CreateSqlAccountAsync_ReturnsErrorWhenManualPasswordIsMissing()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
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

        var command = new CreateSqlAccount(intId, accountId, PasswordSource.Manual, null); // No password
        var result = await service.CreateSqlAccountAsync(command, "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Password is required", result.Error!);
    }

    [Fact]
    public async Task CreateSqlAccountAsync_ReturnsErrorWhenSecretProviderNotLinked()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null), // Plain reference, not AKV
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
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", false, Array.Empty<SqlActualGrant>()), null));
        
        var service = CreateService(store, inspector: mockInspector.Object);

        var command = new CreateSqlAccount(intId, accountId, PasswordSource.SecretProvider, null);
        var result = await service.CreateSqlAccountAsync(command, "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("not linked to a Secret Provider", result.Error!);
    }

    [Fact]
    public async Task CreateSqlAccountAsync_CreatesAccountWithManualPassword()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
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
        // First call: principal doesn't exist
        mockInspector
            .SetupSequence(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", false, Array.Empty<SqlActualGrant>()), null))
            // Second call after creation: principal exists with expected status
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, Array.Empty<SqlActualGrant>()), null));

        mockInspector
            .Setup(i => i.CreatePrincipalAsync(It.IsAny<SqlIntegration>(), "testuser", "testpass123", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new List<SqlAccountCreationOperation>
            {
                new SqlAccountCreationOperation("CREATE LOGIN", null, true, null),
                new SqlAccountCreationOperation("CREATE USER", "TestDB", true, null)
            }, null));
        
        var mockAuditService = new Mock<IAuditService>();
        var service = CreateService(store, inspector: mockInspector.Object, auditService: mockAuditService.Object);

        var command = new CreateSqlAccount(intId, accountId, PasswordSource.Manual, "testpass123");
        var result = await service.CreateSqlAccountAsync(command, "testUser", Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Success);
        Assert.Equal(PasswordSourceUsed.Manual, result.Value.PasswordSource);
        Assert.Equal(2, result.Value.Operations.Count);
        Assert.Contains(result.Value.Operations, o => o.OperationType == "CREATE LOGIN" && o.Success);
        Assert.Contains(result.Value.Operations, o => o.OperationType == "CREATE USER" && o.Database == "TestDB" && o.Success);
        
        // Verify audit log was created
        mockAuditService.Verify(a => a.LogAsync(It.Is<AuditLog>(l => l.Action == AuditAction.SqlAccountCreated), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSqlAccountAsync_CreatesAccountWithSecretProvider()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.AzureKeyVault, null, new AzureKeyVaultBinding(providerId, "testuser-password", null)),
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
            .SetupSequence(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", false, Array.Empty<SqlActualGrant>()), null))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, Array.Empty<SqlActualGrant>()), null));

        mockInspector
            .Setup(i => i.CreatePrincipalAsync(It.IsAny<SqlIntegration>(), "testuser", "secret-password-from-akv", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new List<SqlAccountCreationOperation>
            {
                new SqlAccountCreationOperation("CREATE LOGIN", null, true, null),
                new SqlAccountCreationOperation("CREATE USER", "TestDB", true, null)
            }, null));

        var mockSecretService = new Mock<ISecretOperationService>();
        mockSecretService
            .Setup(s => s.RevealSecretAsync(It.Is<RevealSecret>(r => r.ProviderId == providerId && r.SecretName == "testuser-password"), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync(Result<string>.Success("secret-password-from-akv"));
        
        var mockAuditService = new Mock<IAuditService>();
        var service = CreateService(store, inspector: mockInspector.Object, auditService: mockAuditService.Object, secretOperationService: mockSecretService.Object);

        var command = new CreateSqlAccount(intId, accountId, PasswordSource.SecretProvider, null);
        var result = await service.CreateSqlAccountAsync(command, "testUser", Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Success);
        Assert.Equal(PasswordSourceUsed.SecretProvider, result.Value.PasswordSource);
        Assert.Equal(2, result.Value.Operations.Count);
        
        // Verify secret was retrieved from provider
        mockSecretService.Verify(s => s.RevealSecretAsync(It.IsAny<RevealSecret>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Once);
    }

    [Fact]
    public async Task BulkResolveAsync_ReturnsNotFoundForMissingIntegration()
    {
        var store = NewStore();
        var service = CreateService(store);

        var result = await service.BulkResolveAsync(new BulkResolve(Guid.NewGuid(), BulkPasswordSource.SecretProvider), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task BulkResolveAsync_ReturnsErrorWhenMissingPermissions()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        // Integration only has Read permission, needs Read, Write, and Create
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds });
        var service = CreateService(store);

        var result = await service.BulkResolveAsync(new BulkResolve(intId, BulkPasswordSource.SecretProvider), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("permission", result.Error!.ToLowerInvariant());
    }

    [Fact]
    public async Task BulkResolveAsync_ReturnsEmptyResultsWhenNoAccountsNeedAction()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
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
        
        var mockAuditService = new Mock<IAuditService>();
        var service = CreateService(store, inspector: mockInspector.Object, auditService: mockAuditService.Object);

        var result = await service.BulkResolveAsync(new BulkResolve(intId, BulkPasswordSource.SecretProvider), "testUser", null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Results);
        Assert.Equal(0, result.Value.Summary.TotalProcessed);
    }

    [Fact]
    public async Task BulkResolveAsync_ResolvesDriftForExistingAccounts()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
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
        // Initial check: has SELECT only, needs INSERT too
        mockInspector
            .SetupSequence(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, new List<SqlActualGrant>
            {
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select })
            }), null))
            // After resolve: in sync
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, new List<SqlActualGrant>
            {
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select, Privilege.Insert })
            }), null));

        mockInspector
            .Setup(i => i.ApplyPermissionChangesAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<IReadOnlyList<SqlPermissionComparison>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new List<DriftResolutionOperation>
            {
                new DriftResolutionOperation("GRANT", "TestDB", null, Privilege.Insert, true, null)
            }, null));
        
        var mockAuditService = new Mock<IAuditService>();
        var service = CreateService(store, inspector: mockInspector.Object, auditService: mockAuditService.Object);

        var result = await service.BulkResolveAsync(new BulkResolve(intId, BulkPasswordSource.SecretProvider), "testUser", null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Success);
        Assert.Single(result.Value.Results);
        Assert.Equal("Resolve Drift", result.Value.Results[0].OperationType);
        Assert.True(result.Value.Results[0].Success);
        Assert.Equal(1, result.Value.Summary.DriftsResolved);
    }

    [Fact]
    public async Task BulkResolveAsync_SkipsMissingAccountsWithoutSecretProvider()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
        // Account without Secret Provider binding
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null), // Plain reference, not AKV
            "testuser",
            null,
            new List<Grant> { new Grant(Guid.NewGuid(), "TestDB", null, new HashSet<Privilege> { Privilege.Select }) },
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        // Principal doesn't exist
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", false, Array.Empty<SqlActualGrant>()), null));
        
        var mockAuditService = new Mock<IAuditService>();
        var service = CreateService(store, inspector: mockInspector.Object, auditService: mockAuditService.Object);

        var result = await service.BulkResolveAsync(new BulkResolve(intId, BulkPasswordSource.SecretProvider), "testUser", null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.Results);
        Assert.Equal("Create Account", result.Value.Results[0].OperationType);
        Assert.False(result.Value.Results[0].Success);
        Assert.Contains("skipped", result.Value.Results[0].ErrorMessage!.ToLowerInvariant());
        Assert.Equal(1, result.Value.Summary.Skipped);
    }

    [Fact]
    public async Task BulkResolveAsync_CreatesMissingAccountsWithSecretProvider()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, DateTime.UtcNow, DateTime.UtcNow);
        // Account with Secret Provider binding
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.AzureKeyVault, null, new AzureKeyVaultBinding(providerId, "testuser-password", null)),
            "testuser",
            null,
            new List<Grant> { new Grant(Guid.NewGuid(), "TestDB", null, new HashSet<Privilege> { Privilege.Select }) },
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        // Principal doesn't exist initially
        mockInspector
            .SetupSequence(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            // First call: overview - principal doesn't exist
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", false, Array.Empty<SqlActualGrant>()), null))
            // Second call: during create account check - principal doesn't exist
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", false, Array.Empty<SqlActualGrant>()), null))
            // Third call: after creation - principal exists with SELECT permission (in sync)
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, new List<SqlActualGrant>
            {
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select })
            }), null));

        mockInspector
            .Setup(i => i.CreatePrincipalAsync(It.IsAny<SqlIntegration>(), "testuser", "secret-password", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new List<SqlAccountCreationOperation>
            {
                new SqlAccountCreationOperation("CREATE LOGIN", null, true, null),
                new SqlAccountCreationOperation("CREATE USER", "TestDB", true, null)
            }, null));

        var mockSecretService = new Mock<ISecretOperationService>();
        mockSecretService
            .Setup(s => s.RevealSecretAsync(It.Is<RevealSecret>(r => r.ProviderId == providerId && r.SecretName == "testuser-password"), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync(Result<string>.Success("secret-password"));
        
        var mockAuditService = new Mock<IAuditService>();
        var service = CreateService(store, inspector: mockInspector.Object, auditService: mockAuditService.Object, secretOperationService: mockSecretService.Object);

        var result = await service.BulkResolveAsync(new BulkResolve(intId, BulkPasswordSource.SecretProvider), "testUser", null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Success);
        Assert.NotEmpty(result.Value.Results);
        Assert.Contains(result.Value.Results, r => r.OperationType == "Create Account" && r.Success);
        Assert.Equal(1, result.Value.Summary.AccountsCreated);
        
        // Verify secret was retrieved and audit was logged
        mockSecretService.Verify(s => s.RevealSecretAsync(It.IsAny<RevealSecret>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Once);
        mockAuditService.Verify(a => a.LogAsync(It.Is<AuditLog>(l => l.Action == AuditAction.SqlIntegrationBulkResolved), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportPermissionsAsync_ReturnsNotFoundForMissingIntegration()
    {
        var store = NewStore();
        var service = CreateService(store);

        var result = await service.ImportPermissionsAsync(new ImportPermissions(Guid.NewGuid(), Guid.NewGuid()), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task ImportPermissionsAsync_ReturnsErrorWhenNoReadPermission()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Write, DateTime.UtcNow, DateTime.UtcNow); // No Read permission
        var account = new Account(
            accountId,
            dsId,
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
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        var service = CreateService(store);

        var result = await service.ImportPermissionsAsync(new ImportPermissions(intId, accountId), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Read permission", result.Error!);
    }

    [Fact]
    public async Task ImportPermissionsAsync_ReturnsNotFoundForMissingAccount()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds });
        var service = CreateService(store);

        var result = await service.ImportPermissionsAsync(new ImportPermissions(intId, Guid.NewGuid()), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task ImportPermissionsAsync_ReturnsErrorWhenPrincipalDoesNotExist()
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
            new List<Grant>(),
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

        var result = await service.ImportPermissionsAsync(new ImportPermissions(intId, accountId), "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("does not exist", result.Error!);
    }

    [Fact]
    public async Task ImportPermissionsAsync_ImportsPermissionsSuccessfully()
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
            new List<Grant> { new Grant(Guid.NewGuid(), "OldDB", null, new HashSet<Privilege> { Privilege.Delete }) }, // Old grants
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
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select, Privilege.Insert }),
                new SqlActualGrant("TestDB2", "dbo", new HashSet<Privilege> { Privilege.Execute })
            }), null));
        
        var mockAuditService = new Mock<IAuditService>();
        var service = CreateService(store, inspector: mockInspector.Object, auditService: mockAuditService.Object);

        var result = await service.ImportPermissionsAsync(new ImportPermissions(intId, accountId), "testUser", Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Success);
        Assert.Equal(2, result.Value.ImportedGrants.Count);
        Assert.Contains(result.Value.ImportedGrants, g => g.Database == "TestDB" && g.Privileges.Contains(Privilege.Select));
        Assert.Contains(result.Value.ImportedGrants, g => g.Database == "TestDB2" && g.Schema == "dbo" && g.Privileges.Contains(Privilege.Execute));
        
        // Verify audit log was created
        mockAuditService.Verify(a => a.LogAsync(It.Is<AuditLog>(l => l.Action == AuditAction.SqlPermissionsImported), It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify the account was updated in the store
        var updatedAccount = (await store.GetAsync()).Accounts.First(a => a.Id == accountId);
        Assert.Equal(2, updatedAccount.Grants.Count);
    }

    [Fact]
    public async Task ImportOrphanPrincipalAsync_ReturnsNotFoundForMissingIntegration()
    {
        var store = NewStore();
        var service = CreateService(store);

        var command = new ImportOrphanPrincipal(
            Guid.NewGuid(),
            "orphanuser",
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null)
        );
        var result = await service.ImportOrphanPrincipalAsync(command, "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task ImportOrphanPrincipalAsync_ReturnsErrorWhenPrincipalDoesNotExist()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "orphanuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("orphanuser", false, Array.Empty<SqlActualGrant>()), null));
        
        var service = CreateService(store, inspector: mockInspector.Object);

        var command = new ImportOrphanPrincipal(
            intId,
            "orphanuser",
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null)
        );
        var result = await service.ImportOrphanPrincipalAsync(command, "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task ImportOrphanPrincipalAsync_ReturnsConflictWhenPrincipalAlreadyManaged()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        // Already managed account
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
            "existinguser",
            null,
            new List<Grant>(),
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "existinguser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("existinguser", true, Array.Empty<SqlActualGrant>()), null));
        
        var service = CreateService(store, inspector: mockInspector.Object);

        var command = new ImportOrphanPrincipal(
            intId,
            "existinguser",
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null)
        );
        var result = await service.ImportOrphanPrincipalAsync(command, "testUser", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Contains("already managed", result.Error!);
    }

    [Fact]
    public async Task ImportOrphanPrincipalAsync_ImportsOrphanSuccessfully()
    {
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "orphanuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("orphanuser", true, new List<SqlActualGrant>
            {
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select, Privilege.Insert })
            }), null));
        
        var mockAuditService = new Mock<IAuditService>();
        var service = CreateService(store, inspector: mockInspector.Object, auditService: mockAuditService.Object);

        var command = new ImportOrphanPrincipal(
            intId,
            "orphanuser",
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null)
        );
        var result = await service.ImportOrphanPrincipalAsync(command, "testUser", Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Success);
        Assert.Equal("orphanuser", result.Value.PrincipalName);
        Assert.Single(result.Value.ImportedGrants);
        Assert.Contains(result.Value.ImportedGrants, g => g.Database == "TestDB" && g.Privileges.Contains(Privilege.Select));
        
        // Verify audit log was created
        mockAuditService.Verify(a => a.LogAsync(It.Is<AuditLog>(l => l.Action == AuditAction.SqlOrphanPrincipalImported), It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify the account was created in the store
        var createdAccount = (await store.GetAsync()).Accounts.FirstOrDefault(a => a.UserName == "orphanuser");
        Assert.NotNull(createdAccount);
        Assert.Equal(dsId, createdAccount.TargetId);
        Assert.Equal(TargetKind.DataStore, createdAccount.TargetKind);
        Assert.Equal(AuthKind.UserPassword, createdAccount.AuthKind);
        Assert.Single(createdAccount.Grants);
    }

    [Fact]
    public async Task GetPermissionsOverviewAsync_DetectsOrphanPrincipals()
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
            "manageduser",
            null,
            new List<Grant> { new Grant(Guid.NewGuid(), "TestDB", null, new HashSet<Privilege> { Privilege.Select }) },
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        
        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "manageduser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("manageduser", true, new List<SqlActualGrant>
            {
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select })
            }), null));
        mockInspector
            .Setup(i => i.GetAllPrincipalsAsync(It.IsAny<SqlIntegration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new List<SqlPrincipalPermissions>
            {
                new SqlPrincipalPermissions("manageduser", true, new List<SqlActualGrant>
                {
                    new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select })
                }),
                new SqlPrincipalPermissions("orphanuser1", true, new List<SqlActualGrant>
                {
                    new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Insert, Privilege.Update })
                }),
                new SqlPrincipalPermissions("orphanuser2", true, new List<SqlActualGrant>
                {
                    new SqlActualGrant("OtherDB", null, new HashSet<Privilege> { Privilege.Execute })
                })
            }, null));
        
        var service = CreateService(store, inspector: mockInspector.Object);

        var result = await service.GetPermissionsOverviewAsync(intId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.OrphanPrincipals.Count);
        Assert.Contains(result.Value.OrphanPrincipals, o => o.PrincipalName == "orphanuser1");
        Assert.Contains(result.Value.OrphanPrincipals, o => o.PrincipalName == "orphanuser2");
        Assert.Equal(2, result.Value.Summary.OrphanPrincipalCount);
    }
}
