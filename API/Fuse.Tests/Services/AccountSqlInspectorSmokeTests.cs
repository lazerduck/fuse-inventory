using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Fuse.Tests.Services;

[Collection("SqlServerCollection")]
[Trait("Category", "SqlIntegration")]
public class AccountSqlInspectorSmokeTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private readonly AccountSqlInspector _inspector = new();
    private string _dbName = null!;
    private string _dbConn = null!;

    public AccountSqlInspectorSmokeTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbName = $"FuseSmoke_{Guid.NewGuid():N}";
        _dbConn = await _fixture.CreateTestDatabaseAsync(_dbName);

        await using var conn = new SqlConnection(_dbConn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE Smoke (Id INT PRIMARY KEY IDENTITY(1,1), Name NVARCHAR(50));";
        await cmd.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Inspector_Sees_ReadOnly_Select_Grant()
    {
        var user = $"test_{Guid.NewGuid():N}";
        await _fixture.CreateTestAccountAsync(user, "TestPass123!", _dbName, readOnly: true);

        // Use master/admin connection for inspection to allow cross-db querying
        var integration = new SqlIntegration(
            Guid.NewGuid(), "Smoke", Guid.NewGuid(),
            _fixture.MasterConnectionString, null, SqlPermissions.Read,
            DateTime.UtcNow, DateTime.UtcNow);

        var (ok, perms, err) = await _inspector.GetPrincipalPermissionsAsync(integration, user);

        Assert.True(ok, err);
        Assert.NotNull(perms);
        Assert.True(perms!.Exists);
        var grant = perms.Grants.FirstOrDefault(g => g.Database == _dbName);
        Assert.NotNull(grant);
        Assert.Contains(Privilege.Select, grant!.Privileges);
    }
}
