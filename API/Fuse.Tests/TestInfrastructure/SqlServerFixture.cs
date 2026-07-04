using Microsoft.Data.SqlClient;
using System.Runtime.InteropServices;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;
using Xunit.Sdk;

namespace Fuse.Tests.TestInfrastructure;

[CollectionDefinition("SqlServerCollection")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}

public class SqlServerFixture : IAsyncLifetime
{
    private readonly IContainer _container;
    private readonly bool _reuseContainer;
    private readonly bool _isArm64;
    private const int SqlPort = 1433;
    private const string SqlPassword = "YourStrong@Passw0rd";

    public string MasterConnectionString
    {
        get
        {
            var port = _container.GetMappedPublicPort(SqlPort);
            return $"Server={_container.Hostname},{port};Database=master;User ID=sa;Password={SqlPassword};Encrypt=False;TrustServerCertificate=True;Connection Timeout=30;";
        }
    }

    public SqlServerFixture()
    {
        _reuseContainer = string.Equals(
            Environment.GetEnvironmentVariable("FUSE_TESTCONTAINERS_REUSE"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        _isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        var sqlImage = _isArm64
            ? "mcr.microsoft.com/azure-sql-edge:latest"
            : "mcr.microsoft.com/mssql/server:2022-latest";

        var builder = new ContainerBuilder()
            .WithImage(sqlImage)
            .WithPortBinding(SqlPort, assignRandomHostPort: true)
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_SA_PASSWORD", SqlPassword)
            .WithEnvironment("MSSQL_PID", "Developer");

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
            var preview = await GetContainerLogsPreviewAsync();
            throw new XunitException($"SQL Server container did not start within {timeout.TotalSeconds} seconds. Ensure Docker is running and the image can be pulled. Try: docker pull mcr.microsoft.com/mssql/server:2022-latest\nContainer logs (truncated):\n{preview}");
        }

        try
        {
            await startTask;
        }
        catch
        {
            var preview = await GetContainerLogsPreviewAsync();
            throw new XunitException($"SQL container failed to start.\nContainer logs (truncated):\n{preview}");
        }

        // Post-start readiness: poll until SQL accepts connections
        var maxAttempts = _isArm64 ? 180 : 60;
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
                    var preview = await GetContainerLogsPreviewAsync();
                    throw new XunitException($"SQL Server did not accept connections within {maxAttempts}s after container start.\nContainer logs (truncated):\n{preview}");
                }
            }
        }
    }

    private async Task<string> GetContainerLogsPreviewAsync()
    {
        string combined = string.Empty;
        try
        {
            var (stdout, stderr) = await _container.GetLogsAsync();
            combined = (stdout ?? string.Empty) + "\n" + (stderr ?? string.Empty);
        }
        catch
        {
            // Ignore log retrieval failures; startup error is enough context.
        }

        return combined.Length > 3000 ? combined.Substring(0, 3000) : combined;
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
