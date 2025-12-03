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
            string sanitizedConnectionString;
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                sanitizedConnectionString = builder.ConnectionString;
            }
            catch (ArgumentException ex)
            {
                return (false, SqlPermissions.None, $"Invalid connection string: {ex.Message}");
            }

            await using var connection = new SqlConnection(sanitizedConnectionString);
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
            // Check if user can view logins and users (required for reading account permissions)
            const string query = @"
                SELECT 
                    HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW ANY DATABASE') AS HasViewDatabase,
                    HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW ANY DEFINITION') AS HasViewDefinition,
                    HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW SERVER STATE') AS HasViewServerState";
            
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            
            if (await reader.ReadAsync(ct))
            {
                bool HasPerm(int ordinal)
                    => !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) == 1;

                var hasViewDatabase = HasPerm(0);
                var hasViewDefinition = HasPerm(1);
                var hasViewServerState = HasPerm(2);

                // Can read if they have any of these permissions, or can query sys.server_principals
                if (hasViewDatabase || hasViewDefinition || hasViewServerState)
                    return true;
            }

            // Fallback: Try to read from sys.server_principals (logins) and sys.database_principals (users)
            // If we can read these, we can inspect account permissions
            const string testQuery = @"
                SELECT TOP 1 name FROM sys.server_principals WHERE type IN ('S', 'U', 'G')
                UNION ALL
                SELECT TOP 1 name FROM sys.database_principals WHERE type IN ('S', 'U', 'G')";
            
            await using var testCommand = new SqlCommand(testQuery, connection);
            await testCommand.ExecuteScalarAsync(ct);
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
            // Check if user has permissions to ALTER users/logins or manage role membership
            // These are the permissions needed to modify account permissions
            const string query = @"
                SELECT 
                    HAS_PERMS_BY_NAME(NULL, NULL, 'ALTER ANY USER') AS HasAlterUser,
                    HAS_PERMS_BY_NAME(NULL, NULL, 'ALTER ANY LOGIN') AS HasAlterLogin,
                    HAS_PERMS_BY_NAME(NULL, NULL, 'CONTROL SERVER') AS HasControlServer";
            
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            
            if (await reader.ReadAsync(ct))
            {
                bool HasPerm(int ordinal)
                    => !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) == 1;

                var hasAlterUser = HasPerm(0);
                var hasAlterLogin = HasPerm(1);
                var hasControlServer = HasPerm(2);

                // Can write/alter permissions if they have any of these
                return hasAlterUser || hasAlterLogin || hasControlServer;
            }

            return false;
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
            // Check if user has permissions to create new users/logins
            const string query = @"
                SELECT 
                    HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE USER') AS HasCreateUser,
                    HAS_PERMS_BY_NAME(NULL, NULL, 'ALTER ANY USER') AS HasAlterUser,
                    HAS_PERMS_BY_NAME(NULL, NULL, 'ALTER ANY LOGIN') AS HasAlterLogin,
                    HAS_PERMS_BY_NAME(NULL, NULL, 'CONTROL SERVER') AS HasControlServer";
            
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            
            if (await reader.ReadAsync(ct))
            {
                bool HasPerm(int ordinal)
                    => !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) == 1;

                var hasCreateUser = HasPerm(0);
                var hasAlterUser = HasPerm(1);
                var hasAlterLogin = HasPerm(2);
                var hasControlServer = HasPerm(3);

                // Can create accounts if they have CREATE USER, ALTER ANY LOGIN, or CONTROL SERVER
                return hasCreateUser || hasAlterLogin || hasControlServer;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
