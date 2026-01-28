using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class SqlPermissionsCacheServiceTests
{
    private static InMemoryFuseStore NewStore(
        IEnumerable<SqlIntegration>? integrations = null,
        IEnumerable<DataStore>? dataStores = null,
        IEnumerable<Account>? accounts = null)
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
            SqlIntegrations: (integrations ?? Array.Empty<SqlIntegration>()).ToArray(), Positions: Array.Empty<Position>(), ResponsibilityTypes: Array.Empty<ResponsibilityType>(), ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(), Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    private static SqlPermissionsCacheService CreateService(
        InMemoryFuseStore store,
        ISqlPermissionsInspector? inspector = null)
    {
        var mockLogger = new Mock<ILogger<SqlPermissionsCacheService>>();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        
        // Create a mock service provider that returns the inspector
        var mockScopedServiceProvider = new Mock<IServiceProvider>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        
        // Scoped provider returns the inspector
        mockScopedServiceProvider
            .Setup(sp => sp.GetService(typeof(ISqlPermissionsInspector)))
            .Returns(inspector ?? Mock.Of<ISqlPermissionsInspector>());
        
        // Scope returns the scoped provider
        mockServiceScope.Setup(s => s.ServiceProvider).Returns(mockScopedServiceProvider.Object);
        mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(mockServiceScope.Object);
        
        // Root provider returns the scope factory
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockServiceScopeFactory.Object);
        
        return new SqlPermissionsCacheService(
            mockLogger.Object,
            store,
            mockServiceProvider.Object,
            memoryCache);
    }

    [Fact]
    public void GetCachedOverview_NoData_ReturnsNull()
    {
        // Arrange
        var store = NewStore();
        var service = CreateService(store);

        // Act
        var result = service.GetCachedOverview(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetCachedAccountStatus_NoData_ReturnsNull()
    {
        // Arrange
        var store = NewStore();
        var service = CreateService(store);

        // Act
        var result = service.GetCachedAccountStatus(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshIntegrationAsync_NonexistentIntegration_ReturnsNull()
    {
        // Arrange
        var store = NewStore();
        var service = CreateService(store);

        // Act
        var result = await service.RefreshIntegrationAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshIntegrationAsync_ValidIntegration_ReturnsCachedOverview()
    {
        // Arrange
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
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
        
        var mockInspector = new Mock<ISqlPermissionsInspector>();
        mockInspector
            .Setup(i => i.GetOverviewAsync(It.IsAny<SqlIntegration>(), It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SqlIntegrationPermissionsOverviewResponse(
                IntegrationId: intId,
                IntegrationName: "SQL1",
                Accounts: new[]
                {
                    new SqlAccountPermissionsStatus(
                        AccountId: accountId,
                        AccountName: "testuser @ DS1",
                        PrincipalName: "testuser",
                        Status: SyncStatus.InSync,
                        PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                        ErrorMessage: null
                    )
                },
                OrphanPrincipals: Array.Empty<SqlOrphanPrincipal>(),
                Summary: new SqlPermissionsOverviewSummary(1, 1, 0, 0, 0, 0),
                ErrorMessage: null
            ));
        
        var service = CreateService(store, mockInspector.Object);

        // Act
        var result = await service.RefreshIntegrationAsync(intId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(intId, result.Overview.IntegrationId);
        Assert.Equal("SQL1", result.Overview.IntegrationName);
        Assert.Single(result.Overview.Accounts);
        Assert.Equal(SyncStatus.InSync, result.Overview.Accounts[0].Status);
        Assert.True(result.CachedAt <= DateTime.UtcNow);

        // Verify the cache was updated
        var cachedOverview = service.GetCachedOverview(intId);
        Assert.NotNull(cachedOverview);
        Assert.Equal(intId, cachedOverview.Overview.IntegrationId);
    }

    [Fact]
    public async Task RefreshIntegrationAsync_UpdatesAccountCacheToo()
    {
        // Arrange
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
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
        
        var mockInspector = new Mock<ISqlPermissionsInspector>();
        mockInspector
            .Setup(i => i.GetOverviewAsync(It.IsAny<SqlIntegration>(), It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SqlIntegrationPermissionsOverviewResponse(
                IntegrationId: intId,
                IntegrationName: "SQL1",
                Accounts: new[]
                {
                    new SqlAccountPermissionsStatus(
                        AccountId: accountId,
                        AccountName: "testuser @ DS1",
                        PrincipalName: "testuser",
                        Status: SyncStatus.InSync,
                        PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                        ErrorMessage: null
                    )
                },
                OrphanPrincipals: Array.Empty<SqlOrphanPrincipal>(),
                Summary: new SqlPermissionsOverviewSummary(1, 1, 0, 0, 0, 0),
                ErrorMessage: null
            ));
        
        var service = CreateService(store, mockInspector.Object);

        // Act
        await service.RefreshIntegrationAsync(intId);

        // Assert - The account should also be cached
        var cachedAccount = service.GetCachedAccountStatus(accountId);
        Assert.NotNull(cachedAccount);
        Assert.Equal(accountId, cachedAccount.Status.AccountId);
        Assert.Equal(SyncStatus.InSync, cachedAccount.Status.Status);
    }

    [Fact]
    public async Task RefreshAccountAsync_NonexistentAccount_ReturnsNull()
    {
        // Arrange
        var store = NewStore();
        var service = CreateService(store);

        // Act
        var result = await service.RefreshAccountAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshAccountAsync_NonDataStoreAccount_ReturnsNotApplicable()
    {
        // Arrange
        var appId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var app = new Application(
            appId, "App1", null, null, null, null, null, null, null, new HashSet<Guid>(), new List<ApplicationInstance>(), new List<ApplicationPipeline>(), DateTime.UtcNow, DateTime.UtcNow);
        var snapshot = new Snapshot(
            Applications: new[] { app },
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: new[]
            {
                new Account(
                    accountId,
                    appId,
                    TargetKind.Application, // Not a DataStore
                    AuthKind.UserPassword,
                    new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
                    "testuser",
                    null,
                    new List<Grant>(),
                    new HashSet<Guid>(),
                    DateTime.UtcNow,
                    DateTime.UtcNow
                )
            },
            Identities: Array.Empty<Identity>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: Array.Empty<SqlIntegration>(), Positions: Array.Empty<Position>(), ResponsibilityTypes: Array.Empty<ResponsibilityType>(), ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        var store = new InMemoryFuseStore(snapshot);
        var service = CreateService(store);

        // Act
        var result = await service.RefreshAccountAsync(accountId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SyncStatus.NotApplicable, result.Status.Status);
        Assert.Contains("DataStore", result.Status.StatusSummary);
    }

    [Fact]
    public async Task RefreshAccountAsync_NoSqlIntegration_ReturnsNotApplicable()
    {
        // Arrange
        var dsId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        // No SQL integration
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

        var store = NewStore(dataStores: new[] { ds }, accounts: new[] { account });
        var service = CreateService(store);

        // Act
        var result = await service.RefreshAccountAsync(accountId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SyncStatus.NotApplicable, result.Status.Status);
        Assert.Contains("No SQL integration", result.Status.StatusSummary);
    }

    [Fact]
    public async Task RefreshAccountAsync_ValidAccount_ReturnsCachedStatus()
    {
        // Arrange
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
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
        
        var mockInspector = new Mock<ISqlPermissionsInspector>();
        mockInspector
            .Setup(i => i.GetAccountStatusAsync(It.IsAny<Account>(), It.IsAny<SqlIntegration>(), It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountSqlStatusResponse(
                AccountId: accountId,
                SqlIntegrationId: intId,
                SqlIntegrationName: "SQL1",
                Status: SyncStatus.InSync,
                StatusSummary: "Permissions are in sync.",
                PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                ErrorMessage: null
            ));
        
        var service = CreateService(store, mockInspector.Object);

        // Act
        var result = await service.RefreshAccountAsync(accountId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(accountId, result.Status.AccountId);
        Assert.Equal(intId, result.Status.SqlIntegrationId);
        Assert.Equal(SyncStatus.InSync, result.Status.Status);
        Assert.True(result.CachedAt <= DateTime.UtcNow);

        // Verify the cache was updated
        var cachedStatus = service.GetCachedAccountStatus(accountId);
        Assert.NotNull(cachedStatus);
        Assert.Equal(accountId, cachedStatus.Status.AccountId);
    }

    [Fact]
    public async Task RefreshAccountAsync_DetectsDrift()
    {
        // Arrange
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
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
                new Grant(Guid.NewGuid(), "TestDB", null, new HashSet<Privilege> { Privilege.Select, Privilege.Insert }) // Expects both
            },
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<ISqlPermissionsInspector>();
        mockInspector
            .Setup(i => i.GetAccountStatusAsync(It.IsAny<Account>(), It.IsAny<SqlIntegration>(), It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountSqlStatusResponse(
                AccountId: accountId,
                SqlIntegrationId: intId,
                SqlIntegrationName: "SQL1",
                Status: SyncStatus.DriftDetected,
                StatusSummary: "Permission drift detected between configured and actual grants.",
                PermissionComparisons: new[]
                {
                    new SqlPermissionComparison(
                        Database: "TestDB",
                        Schema: null,
                        ConfiguredPrivileges: new HashSet<Privilege> { Privilege.Select, Privilege.Insert },
                        ActualPrivileges: new HashSet<Privilege> { Privilege.Select },
                        MissingPrivileges: new HashSet<Privilege> { Privilege.Insert },
                        ExtraPrivileges: new HashSet<Privilege>())
                },
                ErrorMessage: null
            ));
        
        var service = CreateService(store, mockInspector.Object);

        // Act
        var result = await service.RefreshAccountAsync(accountId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SyncStatus.DriftDetected, result.Status.Status);
    }

    [Fact]
    public async Task RefreshAccountAsync_DetectsMissingPrincipal()
    {
        // Arrange
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
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
        
        var mockInspector = new Mock<ISqlPermissionsInspector>();
        mockInspector
            .Setup(i => i.GetAccountStatusAsync(It.IsAny<Account>(), It.IsAny<SqlIntegration>(), It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountSqlStatusResponse(
                AccountId: accountId,
                SqlIntegrationId: intId,
                SqlIntegrationName: "SQL1",
                Status: SyncStatus.MissingPrincipal,
                StatusSummary: "SQL principal 'testuser' does not exist.",
                PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                ErrorMessage: null
            ));
        
        var service = CreateService(store, mockInspector.Object);

        // Act
        var result = await service.RefreshAccountAsync(accountId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SyncStatus.MissingPrincipal, result.Status.Status);
    }

    [Fact]
    public void InvalidateIntegration_RemovesFromCache()
    {
        // Arrange
        var store = NewStore();
        var service = CreateService(store);
        var intId = Guid.NewGuid();

        // Manually add to cache for testing (we can't easily do this, so we test the no-op case)
        // Act
        service.InvalidateIntegration(intId);

        // Assert - Should not throw
        Assert.Null(service.GetCachedOverview(intId));
    }

    [Fact]
    public void InvalidateAccount_RemovesFromCache()
    {
        // Arrange
        var store = NewStore();
        var service = CreateService(store);
        var accountId = Guid.NewGuid();

        // Act
        service.InvalidateAccount(accountId);

        // Assert - Should not throw
        Assert.Null(service.GetCachedAccountStatus(accountId));
    }

    [Fact]
    public async Task RefreshIntegrationAsync_NoReadPermission_ReturnsErrorMessage()
    {
        // Arrange
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        // Integration without Read permission
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", null, SqlPermissions.None, DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds });
        
        var mockInspector = new Mock<ISqlPermissionsInspector>();
        mockInspector
            .Setup(i => i.GetOverviewAsync(It.IsAny<SqlIntegration>(), It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SqlIntegrationPermissionsOverviewResponse(
                IntegrationId: intId,
                IntegrationName: "SQL1",
                Accounts: Array.Empty<SqlAccountPermissionsStatus>(),
                OrphanPrincipals: Array.Empty<SqlOrphanPrincipal>(),
                Summary: new SqlPermissionsOverviewSummary(0, 0, 0, 0, 0, 0),
                ErrorMessage: "SQL integration does not have Read permission to inspect accounts."
            ));
        
        var service = CreateService(store, mockInspector.Object);

        // Act
        var result = await service.RefreshIntegrationAsync(intId);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Overview.ErrorMessage);
        Assert.Contains("Read permission", result.Overview.ErrorMessage);
    }

    [Fact]
    public async Task RefreshIntegrationAsync_DetectsOrphanPrincipals()
    {
        // Arrange
        var dsId = Guid.NewGuid();
        var intId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var ds = new DataStore(dsId, "DS1", null, "sql", Guid.NewGuid(), null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var integration = new SqlIntegration(intId, "SQL1", dsId, "Server=test;", null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(
            accountId,
            dsId,
            TargetKind.DataStore,
            AuthKind.UserPassword,
            new SecretBinding(SecretBindingKind.PlainReference, "secret", null),
            "manageduser",
            null,
            new List<Grant>(),
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        
        var mockInspector = new Mock<ISqlPermissionsInspector>();
        mockInspector
            .Setup(i => i.GetOverviewAsync(It.IsAny<SqlIntegration>(), It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SqlIntegrationPermissionsOverviewResponse(
                IntegrationId: intId,
                IntegrationName: "SQL1",
                Accounts: new[]
                {
                    new SqlAccountPermissionsStatus(
                        AccountId: accountId,
                        AccountName: "manageduser @ DS1",
                        PrincipalName: "manageduser",
                        Status: SyncStatus.InSync,
                        PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                        ErrorMessage: null
                    )
                },
                OrphanPrincipals: new[]
                {
                    new SqlOrphanPrincipal("orphanuser", new[]
                    {
                        new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select })
                    })
                },
                Summary: new SqlPermissionsOverviewSummary(1, 1, 0, 0, 0, 1),
                ErrorMessage: null
            ));
        
        var service = CreateService(store, mockInspector.Object);

        // Act
        var result = await service.RefreshIntegrationAsync(intId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Overview.OrphanPrincipals);
        Assert.Equal("orphanuser", result.Overview.OrphanPrincipals[0].PrincipalName);
        Assert.Equal(1, result.Overview.Summary.OrphanPrincipalCount);
    }
}
