using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class SqlPermissionsInspectorTests
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
            SqlIntegrations: (integrations ?? Array.Empty<SqlIntegration>()).ToArray(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task GetAccountStatusAsync_InSync_Works()
    {
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
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("testuser", true, new List<SqlActualGrant>
            {
                new SqlActualGrant("TestDB", null, new HashSet<Privilege> { Privilege.Select })
            }), null));

        var facade = new SqlPermissionsInspector(mockInspector.Object);
        var status = await facade.GetAccountStatusAsync(account, integration, store.Current!, CancellationToken.None);

        Assert.Equal(SyncStatus.InSync, status.Status);
        Assert.Equal(intId, status.SqlIntegrationId);
        Assert.Equal(accountId, status.AccountId);
    }

    [Fact]
    public async Task GetOverviewAsync_ParsesOrphans()
    {
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
            "managed",
            null,
            new List<Grant>(),
            new HashSet<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var store = NewStore(integrations: new[] { integration }, dataStores: new[] { ds }, accounts: new[] { account });
        var mockInspector = new Mock<IAccountSqlInspector>();
        mockInspector
            .Setup(i => i.GetPrincipalPermissionsAsync(It.IsAny<SqlIntegration>(), "managed", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new SqlPrincipalPermissions("managed", true, new List<SqlActualGrant>()), null));
        mockInspector
            .Setup(i => i.GetAllPrincipalsAsync(It.IsAny<SqlIntegration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new List<SqlPrincipalPermissions>
            {
                new SqlPrincipalPermissions("managed", true, new List<SqlActualGrant>()),
                new SqlPrincipalPermissions("orphan", true, new List<SqlActualGrant>())
            }, null));

        var facade = new SqlPermissionsInspector(mockInspector.Object);
        var overview = await facade.GetOverviewAsync(integration, store.Current!, CancellationToken.None);

        Assert.Equal(intId, overview.IntegrationId);
        Assert.Single(overview.OrphanPrincipals);
        Assert.Equal("orphan", overview.OrphanPrincipals[0].PrincipalName);
        Assert.Equal(1, overview.Summary.OrphanPrincipalCount);
    }
}
