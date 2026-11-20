using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

[Collection("SqlServerCollection")]
[Trait("Category", "Integration")]
[Trait("Category", "SqlIntegration")]
public class SqlBulkPreviewIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task ConfirmedPlanCreatesOnlyTheDocumentedUserAndGrants()
    {
        var now = DateTime.UtcNow;
        var database = "BulkPreview_" + Guid.NewGuid().ToString("N");
        var login = "preview_" + Guid.NewGuid().ToString("N");
        var db = await fixture.CreateTestDatabaseAsync(database);
        await using var connection = new SqlConnection(db);
        await connection.OpenAsync();
        await using (var schema = new SqlCommand("CREATE SCHEMA orders", connection)) await schema.ExecuteNonQueryAsync();
        var integration = new SqlIntegration(Guid.NewGuid(), "SQL", Guid.NewGuid(), fixture.MasterConnectionString,
            null, SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, now, now);
        var account = new Account(Guid.NewGuid(), integration.DataStoreId, TargetKind.DataStore, AuthKind.UserPassword,
            new(SecretBindingKind.AzureKeyVault, null, new(Guid.NewGuid(), "login", null)), login, null,
            [new(Guid.NewGuid(), database, null, [Privilege.Connect]), new(Guid.NewGuid(), database, "orders", [Privilege.Select])],
            [], now, now);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { SqlIntegrations = [integration], Accounts = [account] });
        var secrets = new Mock<ISecretOperationService>();
        secrets.Setup(p => p.RevealSecretAsync(It.IsAny<RevealSecret>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync(Result<string>.Success(Guid.NewGuid().ToString("N") + "!Aa7"));
        var service = new SqlIntegrationService(store, Mock.Of<ISqlConnectionValidator>(), new AccountSqlInspector(),
            new FakeAuditService(), secrets.Object);
        var plan = await service.PreviewBulkAsync(integration.Id);
        Assert.True(plan.IsSuccess, plan.Error);
        var operations = plan.Value!.Operations;
        Assert.Equal(new[] { "Create login", "Create user", "Grant" }, operations.Select(o => o.Operation));
        Assert.Equal((database, "orders", Privilege.Select), (operations[2].Database, operations[2].Schema, operations[2].Privilege));
        secrets.VerifyNoOtherCalls();

        var applied = await service.BulkResolveAsync(new(integration.Id, BulkPasswordSource.SecretProvider, plan.Value.Revision), "tester", null);
        Assert.True(applied.IsSuccess, applied.Error);
        Assert.True(applied.Value!.Success, string.Join(", ", applied.Value.Results.Select(r => r.ErrorMessage)));
        Assert.Equal(1, applied.Value.Summary.AccountsCreated);
        Assert.Equal(1, applied.Value.Summary.DriftsResolved);
        var next = await service.PreviewBulkAsync(integration.Id);
        Assert.True(next.IsSuccess, next.Error);
        Assert.Empty(next.Value!.Operations);
        await using var inspect = new SqlCommand("""
            SELECT COUNT(*) FROM sys.database_permissions WHERE grantee_principal_id=USER_ID(@login)
            AND class=3 AND major_id=SCHEMA_ID('orders') AND permission_name='SELECT' AND state='G'
            """, connection);
        inspect.Parameters.AddWithValue("@login", login);
        Assert.Equal(1, await inspect.ExecuteScalarAsync());
    }
}
