using Fuse.Core.Areas.SqlIntegration;
using Fuse.Core.Areas.Logging;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

[Collection("SqlServerCollection")]
[Trait("Category", "Integration")]
[Trait("Category", "SqlIntegration")]
public class SqlConnectionValidatorIntegrationTests
{
    private readonly SqlServerFixture _fixture;
    private readonly SqlConnectionValidator _validator = new(Mock.Of<ILogService>());

    public SqlConnectionValidatorIntegrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("db_owner")]
    [InlineData("db_securityadmin")]
    public async Task ValidateConnection_DatabasePermissionManager_IsWriteWithoutCreate(string role)
    {
        var connectionString = await CreateDatabasePrincipalAsync(role);

        // The connection targets master; authority exists only in the generated user database.
        var (success, permissions, error) = await _validator.ValidateConnectionAsync(connectionString);

        Assert.True(success, error);
        Assert.Equal(SqlPermissions.Read | SqlPermissions.Write, permissions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("db_datareader")]
    [InlineData("db_datawriter")]
    public async Task ValidateConnection_DatabaseAccessOrDataModification_DoesNotAllowPermissionManagement(string? role)
    {
        var connectionString = await CreateDatabasePrincipalAsync(role);

        var (success, permissions, error) = await _validator.ValidateConnectionAsync(connectionString);

        Assert.True(success, error);
        Assert.Equal(SqlPermissions.Read, permissions);
    }

    [Fact]
    public async Task ValidateConnection_LoginManagerWithoutDatabaseAuthority_HasCreateButNotWrite()
    {
        var login = $"capability_login_{Guid.NewGuid():N}";
        await CreateLoginAsync(login);
        await ExecuteAsync(_fixture.MasterConnectionString, $"GRANT ALTER ANY LOGIN TO [{login}];");

        var (success, permissions, error) = await _validator.ValidateConnectionAsync(LoginConnectionString(login));

        Assert.True(success, error);
        Assert.Equal(SqlPermissions.Read | SqlPermissions.Create, permissions);
    }

    [Fact]
    public async Task ValidateConnection_ServerAdministrator_HasAllCapabilities()
    {
        var (success, permissions, error) = await _validator.ValidateConnectionAsync(_fixture.MasterConnectionString);

        Assert.True(success, error);
        Assert.Equal(SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create, permissions);
    }

    private async Task<string> CreateDatabasePrincipalAsync(string? role)
    {
        var database = $"FuseCapabilities_{Guid.NewGuid():N}";
        var databaseConnection = await _fixture.CreateTestDatabaseAsync(database);
        var login = $"capability_user_{Guid.NewGuid():N}";
        await CreateLoginAsync(login);
        await ExecuteAsync(databaseConnection, $@"
            CREATE USER [{login}] FOR LOGIN [{login}];
            GRANT CONNECT TO [{login}];
            {(role is null ? string.Empty : $"ALTER ROLE [{role}] ADD MEMBER [{login}];")}");
        return LoginConnectionString(login);
    }

    private async Task CreateLoginAsync(string login)
    {
        await ExecuteAsync(_fixture.MasterConnectionString, $@"
            CREATE LOGIN [{login}] WITH PASSWORD = 'CapabilityTestPass123!', CHECK_POLICY = OFF;
            GRANT VIEW ANY DATABASE TO [{login}];
            GRANT VIEW ANY DEFINITION TO [{login}];");
    }

    private string LoginConnectionString(string login) => new SqlConnectionStringBuilder(_fixture.MasterConnectionString)
    {
        UserID = login,
        Password = "CapabilityTestPass123!",
        Pooling = false
    }.ConnectionString;

    private static async Task ExecuteAsync(string connectionString, string query)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(query, connection);
        await command.ExecuteNonQueryAsync();
    }
}
