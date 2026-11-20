using Fuse.Core.Areas.Account;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Fuse.Tests.Services;

[Collection("SqlServerCollection")]
[Trait("Category", "Integration")]
[Trait("Category", "SqlIntegration")]
public class AccountSqlInspectorCollationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly AccountSqlInspector _inspector = new();
    private readonly string _atlas = $"Atlas_{Guid.NewGuid():N}";
    private readonly string _liberate = $"Liberate_{Guid.NewGuid():N}";
    private readonly string _login = $"CollationLogin_{Guid.NewGuid():N}";

    private string ConnectionString(string database) => new SqlConnectionStringBuilder(fixture.MasterConnectionString)
    {
        InitialCatalog = database,
        Pooling = false
    }.ConnectionString;

    public async Task InitializeAsync()
    {
        await ExecuteAsync("master", $"CREATE DATABASE [{_atlas}] COLLATE Latin1_General_CI_AS");
        await ExecuteAsync("master", $"CREATE DATABASE [{_liberate}] COLLATE SQL_Latin1_General_CP1_CI_AS");
        await ExecuteAsync("master", $"CREATE LOGIN [{_login}] WITH PASSWORD = '{Guid.NewGuid():N}!aA7'");
        await ExecuteAsync(_atlas, "CREATE SCHEMA [atlas_marker]");
        await ExecuteAsync(_atlas, $"""
            CREATE USER [{_login}] FOR LOGIN [{_login}];
            GRANT SELECT ON SCHEMA::[atlas_marker] TO [public];
            """);
        await ExecuteAsync(_liberate, "CREATE SCHEMA [liberate_marker]");
        await ExecuteAsync(_liberate, $"""
            CREATE USER [{_login}] FOR LOGIN [{_login}];
            GRANT EXECUTE ON SCHEMA::[liberate_marker] TO [public];
            """);
    }

    public async Task DisposeAsync()
    {
        await ExecuteAsync("master", $"DROP DATABASE IF EXISTS [{_atlas}], [{_liberate}]");
        await ExecuteAsync("master", $"IF SUSER_ID('{_login}') IS NOT NULL DROP LOGIN [{_login}]");
    }

    [Theory]
    [InlineData("Atlas")]
    [InlineData("master")]
    public async Task PrincipalLookupUsesServerCatalogFromEitherInitialDatabase(string initialDatabase)
    {
        var integration = Integration(initialDatabase);
        var requested = _login.ToUpperInvariant(); // The fixture's server catalog is case insensitive.
        var missing = _login + "_absent";

        var single = await _inspector.GetPrincipalPermissionsAsync(integration, requested);
        Assert.True(single.IsSuccessful, single.ErrorMessage);
        Assert.NotNull(single.Permissions);
        Assert.True(single.Permissions.Exists);
        Assert.Contains(single.Permissions.Grants, g => g.Database == _atlas && g.Schema == "atlas_marker"
            && g.Privileges.Contains(Privilege.Select));
        Assert.Contains(single.Permissions.Grants, g => g.Database == _liberate && g.Schema == "liberate_marker"
            && g.Privileges.Contains(Privilege.Execute));

        var batch = await _inspector.GetPrincipalPermissionsBatchAsync(integration, [requested, missing]);
        Assert.True(batch.IsSuccessful, batch.ErrorMessage);
        Assert.True(batch.PermissionsMap[requested].Exists);
        Assert.False(batch.PermissionsMap[missing].Exists);
        Assert.Empty(batch.PermissionsMap[missing].Grants);
    }

    [Theory]
    [InlineData("Atlas")]
    [InlineData("master")]
    public async Task NewUserBaselineInspectsOnlyRequestedDatabaseFromEitherInitialDatabase(string initialDatabase)
    {
        var integration = Integration(initialDatabase);

        var baseline = await _inspector.GetNewUserBaselineAsync(integration, [_liberate.ToUpperInvariant()]);
        Assert.NotNull(baseline);
        Assert.Contains(baseline.Grants, g => g.Database == _liberate && g.Schema == "liberate_marker"
            && g.Privileges.Contains(Privilege.Execute));
        Assert.DoesNotContain(baseline.Grants, g => g.Database == _atlas);

        Assert.Null(await _inspector.GetNewUserBaselineAsync(integration, [_liberate, _atlas + "_absent"]));
    }

    private SqlIntegration Integration(string initialDatabase) => new(
        Guid.NewGuid(), "Mixed collation", Guid.NewGuid(), ConnectionString(initialDatabase == "Atlas" ? _atlas : "master"),
        null, SqlPermissions.Read, DateTime.UtcNow, DateTime.UtcNow);

    private async Task ExecuteAsync(string database, string sql)
    {
        await using var connection = new SqlConnection(ConnectionString(database));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
