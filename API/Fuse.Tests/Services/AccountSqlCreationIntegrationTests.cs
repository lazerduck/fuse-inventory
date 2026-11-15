using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Fuse.Tests.Services;

[Collection("SqlServerCollection")]
[Trait("Category", "Integration")]
[Trait("Category", "SqlIntegration")]
public class AccountSqlCreationIntegrationTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;
    private readonly AccountSqlInspector _inspector = new();
    private readonly string _database = $"FuseCreate_{Guid.NewGuid():N}";
    private readonly string _principal = $"quoted]login';--_{Guid.NewGuid():N}";

    public AccountSqlCreationIntegrationTests(SqlServerFixture fixture) => _fixture = fixture;

    private SqlIntegration Integration => new(
        Guid.NewGuid(), "Create regression", Guid.NewGuid(), _fixture.MasterConnectionString,
        null, SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create,
        DateTime.UtcNow, DateTime.UtcNow);

    public async Task InitializeAsync() => await _fixture.CreateTestDatabaseAsync(_database);

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(_fixture.MasterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}]; " +
            $"IF SUSER_ID(@principal) IS NOT NULL DROP LOGIN [{_principal.Replace("]", "]]")}];";
        command.Parameters.AddWithValue("@principal", _principal);
        await command.ExecuteNonQueryAsync();
    }

    [Theory]
    [InlineData("TestStrong1!Password")]
    [InlineData("Test'Strong2!Password")]
    [InlineData("Strong3!'; CREATE LOGIN [UnexpectedInjectedLogin] WITH PASSWORD='OtherPass4!'; --")]
    public async Task CreatePrincipal_PreservesLiteralPasswordAndQuotedIdentifier(string password)
    {
        var result = await _inspector.CreatePrincipalAsync(Integration, _principal, password, [_database]);

        Assert.True(result.IsSuccessful, result.ErrorMessage + ": " + string.Join("; ", result.Operations.Select(o => o.ErrorMessage)));
        Assert.Collection(result.Operations,
            login => { Assert.Equal("CREATE LOGIN", login.OperationType); Assert.True(login.Success); },
            user => { Assert.Equal("CREATE USER", user.OperationType); Assert.True(user.Success); });

        // Authenticate using the exact supplied password, not just catalog existence.
        var builder = new SqlConnectionStringBuilder(_fixture.MasterConnectionString)
        {
            InitialCatalog = _database, UserID = _principal, Password = password, Pooling = false
        };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ORIGINAL_LOGIN();";
        Assert.Equal(_principal, await command.ExecuteScalarAsync());

        await using var admin = new SqlConnection(_fixture.MasterConnectionString);
        await admin.OpenAsync();
        await using var check = admin.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sys.server_principals WHERE name=N'UnexpectedInjectedLogin';";
        Assert.Equal(0, Convert.ToInt32(await check.ExecuteScalarAsync()));

        var duplicate = await _inspector.CreatePrincipalAsync(Integration, _principal, password, [_database]);
        Assert.False(duplicate.IsSuccessful);
        Assert.DoesNotContain(password, duplicate.ErrorMessage ?? "");
        Assert.All(duplicate.Operations, operation => Assert.DoesNotContain(password, operation.ErrorMessage ?? ""));
    }

    [Fact]
    public async Task CreatePrincipal_RejectsOverlongIdentifierWithoutReportingSuccess()
    {
        var result = await _inspector.CreatePrincipalAsync(Integration, new string('a', 129), "TestStrong1!Password", [_database]);
        Assert.False(result.IsSuccessful);
        Assert.Empty(result.Operations);
        Assert.Contains("128", result.ErrorMessage);
    }
}
