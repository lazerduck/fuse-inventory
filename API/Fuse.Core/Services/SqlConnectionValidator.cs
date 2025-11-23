using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.Data.SqlClient;

namespace Fuse.Core.Services;

public class SqlConnectionValidator : ISqlConnectionValidator
{
    public async Task<(bool IsSuccessful, SqlPermissions Permissions, string? ErrorMessage)> ValidateConnectionAsync(
        string connectionString, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return (false, SqlPermissions.None, "Connection string is required.");
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            var permissions = SqlPermissions.None;

            // Test Read permission
            if (await TestReadPermissionAsync(connection, ct))
            {
                permissions |= SqlPermissions.Read;
            }

            // Test Write permission
            if (await TestWritePermissionAsync(connection, ct))
            {
                permissions |= SqlPermissions.Write;
            }

            // Test Create permission
            if (await TestCreatePermissionAsync(connection, ct))
            {
                permissions |= SqlPermissions.Create;
            }

            return (true, permissions, null);
        }
        catch (SqlException ex)
        {
            return (false, SqlPermissions.None, $"SQL connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, SqlPermissions.None, $"Connection failed: {ex.Message}");
        }
    }

    private async Task<bool> TestReadPermissionAsync(SqlConnection connection, CancellationToken ct)
    {
        try
        {
            // Try to read from system tables - this should work with minimal permissions
            const string query = "SELECT TOP 1 name FROM sys.databases WHERE database_id = DB_ID()";
            await using var command = new SqlCommand(query, connection);
            await command.ExecuteScalarAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TestWritePermissionAsync(SqlConnection connection, CancellationToken ct)
    {
        try
        {
            // Try to create a temporary table and insert data
            const string createTableQuery = @"
                CREATE TABLE #FusePermissionTest (
                    Id INT PRIMARY KEY,
                    TestValue NVARCHAR(50)
                )";
            
            await using (var command = new SqlCommand(createTableQuery, connection))
            {
                await command.ExecuteNonQueryAsync(ct);
            }

            const string insertQuery = "INSERT INTO #FusePermissionTest (Id, TestValue) VALUES (1, 'test')";
            await using (var command = new SqlCommand(insertQuery, connection))
            {
                await command.ExecuteNonQueryAsync(ct);
            }

            const string dropTableQuery = "DROP TABLE #FusePermissionTest";
            await using (var command = new SqlCommand(dropTableQuery, connection))
            {
                await command.ExecuteNonQueryAsync(ct);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TestCreatePermissionAsync(SqlConnection connection, CancellationToken ct)
    {
        try
        {
            // Check if user has user management permissions (CREATE USER, ALTER ANY USER)
            // These are the permissions needed for future account creation features
            const string query = @"
                SELECT HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE USER') AS HasCreateUser,
                       HAS_PERMS_BY_NAME(NULL, NULL, 'ALTER ANY USER') AS HasAlterUser,
                       HAS_PERMS_BY_NAME(NULL, NULL, 'ALTER ANY LOGIN') AS HasAlterLogin";
            
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            
            if (await reader.ReadAsync(ct))
            {
                var hasCreateUser = reader.GetInt32(0) == 1;
                var hasAlterUser = reader.GetInt32(1) == 1;
                var hasAlterLogin = reader.GetInt32(2) == 1;
                // User needs at least one of these permissions for account management
                return hasCreateUser || hasAlterUser || hasAlterLogin;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
