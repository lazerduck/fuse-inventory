using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using DotNet.Testcontainers.Builders;
using Xunit;
using Xunit.Sdk;

namespace Fuse.Tests.TestInfrastructure;

[CollectionDefinition("SqlServerCollection")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}

public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;
    private readonly bool _reuseContainer;

    public string MasterConnectionString => _container.GetConnectionString();

    public SqlServerFixture()
    {
        _reuseContainer = string.Equals(
            Environment.GetEnvironmentVariable("FUSE_TESTCONTAINERS_REUSE"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        var builder = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong@Passw0rd")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433));

        if (_reuseContainer)
        {
            builder = builder
                .WithName("fuse-tests-mssql")
                .WithReuse(true);
        }

        _container = builder.Build();
    }

    public async Task InitializeAsync()
    {
        var timeout = TimeSpan.FromMinutes(3);
        var startTask = _container.StartAsync();
        var completed = await Task.WhenAny(startTask, Task.Delay(timeout));
        if (completed != startTask)
        {
            string combined = string.Empty;
            try
            {
                var (stdout, stderr) = await _container.GetLogsAsync();
                combined = (stdout ?? string.Empty) + "\n" + (stderr ?? string.Empty);
            }
            catch { /* ignore */ }
            var preview = combined.Length > 2000 ? combined.Substring(0, 2000) : combined;
            throw new XunitException($"SQL Server container did not start within {timeout.TotalSeconds} seconds. Ensure Docker is running and the image can be pulled. Try: docker pull mcr.microsoft.com/mssql/server:2022-latest\nContainer logs (truncated):\n{preview}");
        }
        await startTask;

        // Post-start readiness: poll until SQL accepts connections
        var maxAttempts = 60;
        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                await using var probe = new SqlConnection(MasterConnectionString);
                await probe.OpenAsync();
                await probe.CloseAsync();
                break; // Ready
            }
            catch
            {
                await Task.Delay(1000);
                if (i == maxAttempts - 1)
                {
                    throw new XunitException("SQL Server did not accept connections within 60s after container start.");
                }
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_reuseContainer)
        {
            return;
        }

        await _container.DisposeAsync();
    }

    public async Task<string> CreateTestDatabaseAsync(string databaseName)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
IF EXISTS (SELECT * FROM sys.databases WHERE name = '{databaseName}')
BEGIN
    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{databaseName}];
END
CREATE DATABASE [{databaseName}];";
        await cmd.ExecuteNonQueryAsync();

        var builder = new SqlConnectionStringBuilder(MasterConnectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    public async Task CreateTestAccountAsync(string loginName, string password, string databaseName, bool readOnly = true)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();

        await using (var loginCmd = connection.CreateCommand())
        {
            loginCmd.CommandText = $@"
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = '{loginName}')
BEGIN
    CREATE LOGIN [{loginName}] WITH PASSWORD = '{password}', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
END";
            await loginCmd.ExecuteNonQueryAsync();
        }

        await using (var userCmd = connection.CreateCommand())
        {
            userCmd.CommandText = $@"
USE [{databaseName}];
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '{loginName}')
BEGIN
    CREATE USER [{loginName}] FOR LOGIN [{loginName}];
END
ALTER ROLE db_datareader ADD MEMBER [{loginName}];
{(readOnly ? string.Empty : "ALTER ROLE db_datawriter ADD MEMBER [" + loginName + "];")}
GRANT CONNECT TO [{loginName}];";
            await userCmd.ExecuteNonQueryAsync();
        }
    }
}
