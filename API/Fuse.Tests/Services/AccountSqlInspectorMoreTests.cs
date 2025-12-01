using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Fuse.Tests.Services;

[Collection("SqlServerCollection")]
[Trait("Category", "SqlIntegration")]
public class AccountSqlInspectorMoreTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private readonly AccountSqlInspector _inspector = new();
    private string _dbName = null!;
    private string _dbConn = null!;

    public AccountSqlInspectorMoreTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbName = $"FuseIT_{Guid.NewGuid():N}";
        _dbConn = await _fixture.CreateTestDatabaseAsync(_dbName);

        await using var conn = new SqlConnection(_dbConn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE T (Id INT PRIMARY KEY IDENTITY(1,1), Name NVARCHAR(50));";
        await cmd.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReadWriteUser_Has_Select_And_Insert()
    {
        var user = $"rw_{Guid.NewGuid():N}";
        await _fixture.CreateTestAccountAsync(user, "TestPass123!", _dbName, readOnly: false);

        var integration = new SqlIntegration(
            Guid.NewGuid(), "RW", Guid.NewGuid(),
            _fixture.MasterConnectionString, null, SqlPermissions.Read,
            DateTime.UtcNow, DateTime.UtcNow);

        var (ok, perms, err) = await _inspector.GetPrincipalPermissionsAsync(integration, user);

        Assert.True(ok, err);
        Assert.True(perms!.Exists);
        var grant = perms.Grants.FirstOrDefault(g => g.Database == _dbName);
        Assert.NotNull(grant);
        Assert.Contains(Privilege.Select, grant!.Privileges);
        Assert.Contains(Privilege.Insert, grant.Privileges);
    }

    [Fact]
    public async Task Principal_NonExistent_Returns_NotExists()
    {
        var integration = new SqlIntegration(
            Guid.NewGuid(), "NX", Guid.NewGuid(),
            _fixture.MasterConnectionString, null, SqlPermissions.Read,
            DateTime.UtcNow, DateTime.UtcNow);

        var (ok, perms, err) = await _inspector.GetPrincipalPermissionsAsync(integration, $"nope_{Guid.NewGuid():N}");

        Assert.True(ok, err);
        Assert.NotNull(perms);
        Assert.False(perms!.Exists);
        Assert.Empty(perms.Grants);
    }

    [Fact]
    public async Task User_In_Two_Databases_Returns_Both()
    {
        var user = $"multi_{Guid.NewGuid():N}";
        // First DB: read-only
        await _fixture.CreateTestAccountAsync(user, "TestPass123!", _dbName, readOnly: true);

        // Second DB: create and grant read-write
        var secondDb = $"FuseIT2_{Guid.NewGuid():N}";
        var secondConn = await _fixture.CreateTestDatabaseAsync(secondDb);
        await using (var conn = new SqlConnection(secondConn))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE T2 (Id INT PRIMARY KEY IDENTITY(1,1), Name NVARCHAR(50));";
            await cmd.ExecuteNonQueryAsync();
        }
        await _fixture.CreateTestAccountAsync(user, "TestPass123!", secondDb, readOnly: false);

        var integration = new SqlIntegration(
            Guid.NewGuid(), "Multi", Guid.NewGuid(),
            _fixture.MasterConnectionString, null, SqlPermissions.Read,
            DateTime.UtcNow, DateTime.UtcNow);

        var (ok, perms, err) = await _inspector.GetPrincipalPermissionsAsync(integration, user);

        Assert.True(ok, err);
        Assert.True(perms!.Exists);
        var grant1 = perms.Grants.FirstOrDefault(g => g.Database == _dbName);
        var grant2 = perms.Grants.FirstOrDefault(g => g.Database == secondDb);
        Assert.NotNull(grant1);
        Assert.NotNull(grant2);
        Assert.Contains(Privilege.Select, grant1!.Privileges);
        Assert.Contains(Privilege.Select, grant2!.Privileges);
        Assert.Contains(Privilege.Insert, grant2.Privileges);
    }
}
