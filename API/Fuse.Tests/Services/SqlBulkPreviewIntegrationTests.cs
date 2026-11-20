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
            [new(Guid.NewGuid(), database, null, [Privilege.Connect, Privilege.Select]), new(Guid.NewGuid(), database, "orders", [Privilege.Update])],
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
        Assert.DoesNotContain(operations, op => op.Operation == "Skip");
        Assert.Equal(new[] { "Create login", "Create user", "Grant" }, operations.Select(o => o.Operation));
        Assert.Equal((database, (string?)null, Privilege.Select), (operations[2].Database, operations[2].Schema, operations[2].Privilege));
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
            AND class=0 AND permission_name='SELECT' AND state='G'
            """, connection);
        inspect.Parameters.AddWithValue("@login", login);
        Assert.Equal(1, await inspect.ExecuteScalarAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BulkResolvesDatabaseDriftAndKeepsProcedureColumnAndDeniedPermissions(bool sendPreviewRevision)
    {
        var database = "BulkScope_" + Guid.NewGuid().ToString("N");
        var login = "scoped_" + Guid.NewGuid().ToString("N");
        var connectionString = await fixture.CreateTestDatabaseAsync(database);
        await fixture.CreateTestAccountAsync(login, Guid.NewGuid().ToString("N") + "!Aa7", database);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var setup = new SqlCommand($"""
            ALTER ROLE db_datareader DROP MEMBER [{login}];
            CREATE TABLE dbo.Fields (Visible int, PrivateValue int);
            EXEC('CREATE PROCEDURE dbo.Report AS SELECT 1');
            GRANT EXECUTE ON OBJECT::dbo.Report TO [{login}];
            GRANT SELECT ON OBJECT::dbo.Fields (Visible) TO [{login}];
            DENY SELECT ON OBJECT::dbo.Fields (PrivateValue) TO [{login}];
            DENY DELETE TO [{login}];
            GRANT UPDATE TO [{login}];
            """, connection);
        await setup.ExecuteNonQueryAsync();
        async Task<string[]> ProtectedRows()
        {
            await using var query = new SqlCommand("""
                SELECT CONCAT(class, '/', major_id, '/', minor_id, '/', permission_name, '/', state, '/', grantor_principal_id)
                FROM sys.database_permissions
                WHERE grantee_principal_id=USER_ID(@login) AND (class <> 0 OR permission_name='DELETE')
                ORDER BY class, major_id, minor_id, permission_name
                """, connection);
            query.Parameters.AddWithValue("@login", login);
            var rows = new List<string>();
            await using var reader = await query.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add(reader.GetString(0));
            return rows.ToArray();
        }
        var before = await ProtectedRows();
        Assert.Equal(4, before.Length);
        var now = DateTime.UtcNow;
        var integration = new SqlIntegration(Guid.NewGuid(), "SQL", Guid.NewGuid(), fixture.MasterConnectionString,
            null, SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, now, now);
        var account = new Account(Guid.NewGuid(), integration.DataStoreId, TargetKind.DataStore, AuthKind.UserPassword,
            new(SecretBindingKind.PlainReference, null, null), login, null,
            [new(Guid.NewGuid(), database, null, [Privilege.Connect, Privilege.Insert, Privilege.Delete])], [], now, now);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { SqlIntegrations = [integration], Accounts = [account] });
        var service = new SqlIntegrationService(store, Mock.Of<ISqlConnectionValidator>(), new AccountSqlInspector(),
            new FakeAuditService(), Mock.Of<ISecretOperationService>());
        var plan = await service.PreviewBulkAsync(integration.Id);
        Assert.True(plan.IsSuccess, plan.Error);
        var operations = plan.Value!.Operations;
        Assert.True(operations.Count == 3, string.Join(", ", operations));
        Assert.All(operations, op => { Assert.Null(op.Schema); Assert.Equal(database, op.Database); });
        Assert.Contains(operations, op => op.Operation == "Grant" && op.Privilege == Privilege.Insert);
        Assert.Contains(operations, op => op.Operation == "Revoke" && op.Privilege == Privilege.Update);
        Assert.Contains(operations, op => op.Operation == "Skip" && op.Privilege == Privilege.Delete);
        var applied = await service.BulkResolveAsync(new(integration.Id, BulkPasswordSource.SecretProvider,
            sendPreviewRevision ? plan.Value.Revision : null), "tester", null);
        Assert.True(applied.IsSuccess, applied.Error);
        Assert.Equal(1, applied.Value!.Summary.DriftsResolved);
        Assert.Equal(1, applied.Value.Summary.Skipped);
        Assert.Equal(0, applied.Value.Summary.Failed);
        Assert.Equal(before, await ProtectedRows());
        var next = await service.PreviewBulkAsync(integration.Id);
        Assert.True(next.IsSuccess, next.Error);
        Assert.Equal("Skip", Assert.Single(next.Value!.Operations).Operation);
    }
}
