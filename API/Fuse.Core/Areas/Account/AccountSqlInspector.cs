using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Microsoft.Data.SqlClient;

namespace Fuse.Core.Areas.Account;

/// <summary>
/// Inspects SQL Server principals and their permissions.
/// </summary>
public partial class AccountSqlInspector : IAccountSqlInspector
{
    /// <inheritdoc />
    public async Task<(bool IsSuccessful, IReadOnlyList<string> PrincipalNames, string? ErrorMessage)> GetAllPrincipalNamesAsync(
        Models.SqlIntegration sqlIntegration,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sqlIntegration.ConnectionString))
        {
            return (false, Array.Empty<string>(), "SQL integration has no connection string configured.");
        }

        try
        {
            string sanitizedConnectionString;
            try
            {
                var builder = new SqlConnectionStringBuilder(sqlIntegration.ConnectionString);
                sanitizedConnectionString = builder.ConnectionString;
            }
            catch (ArgumentException ex)
            {
                return (false, Array.Empty<string>(), $"Invalid connection string: {ex.Message}");
            }

            await using var connection = new SqlConnection(sanitizedConnectionString);
            await connection.OpenAsync(ct);

            // Get all server logins (excluding system accounts)
            const string loginQuery = @"
                SELECT name 
                FROM sys.server_principals 
                WHERE type IN ('S', 'U', 'G', 'E', 'X')
                  AND name NOT LIKE '##%'
                  AND name NOT LIKE 'NT %'
                  AND name NOT IN ('sa', 'public', 'guest')
                ORDER BY name";

            var principalNames = new List<string>();
            await using (var command = new SqlCommand(loginQuery, connection))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    principalNames.Add(reader.GetString(0));
                }
            }

            return (true, principalNames, null);
        }
        catch (SqlException ex)
        {
            return (false, Array.Empty<string>(), $"SQL error: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, Array.Empty<string>(), $"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsSuccessful, SqlPrincipalPermissions? Permissions, string? ErrorMessage)> GetPrincipalPermissionsAsync(
        Models.SqlIntegration sqlIntegration,
        string principalName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(principalName))
        {
            return (false, null, "Principal name is required.");
        }

        if (string.IsNullOrWhiteSpace(sqlIntegration.ConnectionString))
        {
            return (false, null, "SQL integration has no connection string configured.");
        }

        try
        {
            string sanitizedConnectionString;
            try
            {
                var builder = new SqlConnectionStringBuilder(sqlIntegration.ConnectionString);
                sanitizedConnectionString = builder.ConnectionString;
            }
            catch (ArgumentException ex)
            {
                return (false, null, $"Invalid connection string: {ex.Message}");
            }

            await using var connection = new SqlConnection(sanitizedConnectionString);
            await connection.OpenAsync(ct);

            var permissions = await ReadPrincipalPermissionsAsync(connection, new[] { principalName }, ct);
            return (true, permissions[principalName], null);
        }
        catch (SqlException ex)
        {
            return (false, null, $"SQL error: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, null, $"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsSuccessful, IReadOnlyList<DriftResolutionOperation> Operations, string? ErrorMessage)> ApplyPermissionChangesAsync(
        Models.SqlIntegration sqlIntegration,
        string principalName,
        IReadOnlyList<SqlPermissionComparison> permissionComparisons,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(principalName))
        {
            return (false, Array.Empty<DriftResolutionOperation>(), "Principal name is required.");
        }

        if (string.IsNullOrWhiteSpace(sqlIntegration.ConnectionString))
        {
            return (false, Array.Empty<DriftResolutionOperation>(), "SQL integration has no connection string configured.");
        }

        // Check if integration has write permissions
        if ((sqlIntegration.Permissions & SqlPermissions.Write) == 0)
        {
            return (false, Array.Empty<DriftResolutionOperation>(), "SQL integration does not have Write permission to modify grants.");
        }

        var operations = new List<DriftResolutionOperation>();
        bool hasFailures = false;

        try
        {
            string sanitizedConnectionString;
            try
            {
                var builder = new SqlConnectionStringBuilder(sqlIntegration.ConnectionString);
                sanitizedConnectionString = builder.ConnectionString;
            }
            catch (ArgumentException ex)
            {
                return (false, Array.Empty<DriftResolutionOperation>(), $"Invalid connection string: {ex.Message}");
            }

            await using var connection = new SqlConnection(sanitizedConnectionString);
            await connection.OpenAsync(ct);

            foreach (var comparison in permissionComparisons)
            {
                // Process missing privileges (need to GRANT)
                foreach (var privilege in comparison.MissingPrivileges)
                {
                    var result = await ApplyGrantAsync(connection, principalName, comparison.Database, comparison.Schema, privilege, ct);
                    operations.Add(result);
                    if (!result.Success)
                    {
                        hasFailures = true;
                    }
                }

                // Process extra privileges (need to REVOKE)
                foreach (var privilege in comparison.ExtraPrivileges)
                {
                    var result = await ApplyRevokeAsync(connection, principalName, comparison.Database, comparison.Schema, privilege, ct);
                    operations.Add(result);
                    if (!result.Success)
                    {
                        hasFailures = true;
                    }
                }
            }

            return (!hasFailures, operations, hasFailures ? "Some operations failed. Check individual operation results." : null);
        }
        catch (SqlException ex)
        {
            return (false, operations, $"SQL error: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, operations, $"Unexpected error: {ex.Message}");
        }
    }

    private static async Task<DriftResolutionOperation> ApplyGrantAsync(
        SqlConnection connection,
        string principalName,
        string? database,
        string? schema,
        Privilege privilege,
        CancellationToken ct)
    {
        var sqlPermission = MapPrivilegeToSqlPermission(privilege);
        if (sqlPermission is null)
        {
            return new DriftResolutionOperation(
                OperationType: "GRANT",
                Database: database,
                Schema: schema,
                Privilege: privilege,
                Success: false,
                ErrorMessage: $"Privilege '{privilege}' cannot be mapped to a SQL permission.");
        }

        try
        {
            // Build the GRANT statement
            var sql = BuildGrantStatement(principalName, database, schema, sqlPermission);
            
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(ct);

            return new DriftResolutionOperation(
                OperationType: "GRANT",
                Database: database,
                Schema: schema,
                Privilege: privilege,
                Success: true,
                ErrorMessage: null);
        }
        catch (SqlException ex)
        {
            return new DriftResolutionOperation(
                OperationType: "GRANT",
                Database: database,
                Schema: schema,
                Privilege: privilege,
                Success: false,
                ErrorMessage: ex.Message);
        }
    }

    private static async Task<DriftResolutionOperation> ApplyRevokeAsync(
        SqlConnection connection,
        string principalName,
        string? database,
        string? schema,
        Privilege privilege,
        CancellationToken ct)
    {
        var sqlPermission = MapPrivilegeToSqlPermission(privilege);
        if (sqlPermission is null)
        {
            return new DriftResolutionOperation(
                OperationType: "REVOKE",
                Database: database,
                Schema: schema,
                Privilege: privilege,
                Success: false,
                ErrorMessage: $"Privilege '{privilege}' cannot be mapped to a SQL permission.");
        }

        try
        {
            // Build the REVOKE statement
            var sql = BuildRevokeStatement(principalName, database, schema, sqlPermission);
            
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(ct);

            return new DriftResolutionOperation(
                OperationType: "REVOKE",
                Database: database,
                Schema: schema,
                Privilege: privilege,
                Success: true,
                ErrorMessage: null);
        }
        catch (SqlException ex)
        {
            return new DriftResolutionOperation(
                OperationType: "REVOKE",
                Database: database,
                Schema: schema,
                Privilege: privilege,
                Success: false,
                ErrorMessage: ex.Message);
        }
    }

    // Whitelist of valid SQL permission names to prevent injection
    private static readonly HashSet<string> ValidPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "EXECUTE", "CONNECT", "ALTER", "CONTROL"
    };

    private static string BuildGrantStatement(string principalName, string? database, string? schema, string permission)
    {
        // Validate permission against whitelist to prevent SQL injection
        if (!ValidPermissions.Contains(permission))
        {
            throw new ArgumentException($"Invalid permission: {permission}", nameof(permission));
        }

        // Escape identifiers to prevent SQL injection
        var escapedPrincipal = EscapeIdentifier(principalName);
        
        if (!string.IsNullOrWhiteSpace(database) && !string.IsNullOrWhiteSpace(schema))
        {
            var escapedDatabase = EscapeIdentifier(database);
            var escapedSchema = EscapeIdentifier(schema);
            return $"USE {escapedDatabase}; GRANT {permission} ON SCHEMA::{escapedSchema} TO {escapedPrincipal};";
        }
        else if (!string.IsNullOrWhiteSpace(database))
        {
            var escapedDatabase = EscapeIdentifier(database);
            return $"USE {escapedDatabase}; GRANT {permission} TO {escapedPrincipal};";
        }
        else
        {
            // Server-level grant (not typically supported for most permissions)
            return $"GRANT {permission} TO {escapedPrincipal};";
        }
    }

    private static string BuildRevokeStatement(string principalName, string? database, string? schema, string permission)
    {
        // Validate permission against whitelist to prevent SQL injection
        if (!ValidPermissions.Contains(permission))
        {
            throw new ArgumentException($"Invalid permission: {permission}", nameof(permission));
        }

        // Escape identifiers to prevent SQL injection
        var escapedPrincipal = EscapeIdentifier(principalName);
        
        if (!string.IsNullOrWhiteSpace(database) && !string.IsNullOrWhiteSpace(schema))
        {
            var escapedDatabase = EscapeIdentifier(database);
            var escapedSchema = EscapeIdentifier(schema);
            return $"USE {escapedDatabase}; REVOKE {permission} ON SCHEMA::{escapedSchema} FROM {escapedPrincipal};";
        }
        else if (!string.IsNullOrWhiteSpace(database))
        {
            var escapedDatabase = EscapeIdentifier(database);
            return $"USE {escapedDatabase}; REVOKE {permission} FROM {escapedPrincipal};";
        }
        else
        {
            // Server-level revoke (not typically supported for most permissions)
            return $"REVOKE {permission} FROM {escapedPrincipal};";
        }
    }

    private static string EscapeIdentifier(string identifier)
    {
        // Use QUOTENAME-style escaping: replace ] with ]] and wrap in brackets
        return "[" + identifier.Replace("]", "]]") + "]";
    }

    private static string? MapPrivilegeToSqlPermission(Privilege privilege)
    {
        return privilege switch
        {
            Privilege.Select => "SELECT",
            Privilege.Insert => "INSERT",
            Privilege.Update => "UPDATE",
            Privilege.Delete => "DELETE",
            Privilege.Execute => "EXECUTE",
            Privilege.Connect => "CONNECT",
            Privilege.Alter => "ALTER",
            Privilege.Control => "CONTROL",
            _ => null
        };
    }

    private static Privilege? MapSqlPermissionToPrivilege(string sqlPermission)
    {
        // Map SQL Server permission names to Fuse Privilege enum
        return sqlPermission.ToUpperInvariant() switch
        {
            "SELECT" => Privilege.Select,
            "INSERT" => Privilege.Insert,
            "UPDATE" => Privilege.Update,
            "DELETE" => Privilege.Delete,
            "EXECUTE" => Privilege.Execute,
            "CONNECT" => Privilege.Connect,
            "ALTER" => Privilege.Alter,
            "CONTROL" => Privilege.Control,
            // Additional mappings for common SQL permissions
            "ALTER ANY SCHEMA" => Privilege.Alter,
            "ALTER ANY USER" => Privilege.Alter,
            "VIEW DEFINITION" => Privilege.Select,
            _ => null // Unmapped permissions are ignored
        };
    }

    /// <inheritdoc />
    public async Task<(bool IsSuccessful, IReadOnlyList<SqlAccountCreationOperation> Operations, string? ErrorMessage)> CreatePrincipalAsync(
        Models.SqlIntegration sqlIntegration,
        string principalName,
        string password,
        IReadOnlyList<string> databases,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(principalName))
        {
            return (false, Array.Empty<SqlAccountCreationOperation>(), "Principal name is required.");
        }

        // QUOTENAME returns NULL for identifiers longer than SQL Server's sysname limit.
        if (principalName.Length > 128)
        {
            return (false, Array.Empty<SqlAccountCreationOperation>(), "Principal name must not exceed 128 characters.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, Array.Empty<SqlAccountCreationOperation>(), "Password is required.");
        }

        if (string.IsNullOrWhiteSpace(sqlIntegration.ConnectionString))
        {
            return (false, Array.Empty<SqlAccountCreationOperation>(), "SQL integration has no connection string configured.");
        }

        // Check if integration has create permissions
        if ((sqlIntegration.Permissions & SqlPermissions.Create) == 0)
        {
            return (false, Array.Empty<SqlAccountCreationOperation>(), "SQL integration does not have Create permission to create logins/users.");
        }

        var operations = new List<SqlAccountCreationOperation>();
        bool hasFailures = false;

        try
        {
            string sanitizedConnectionString;
            try
            {
                var builder = new SqlConnectionStringBuilder(sqlIntegration.ConnectionString);
                sanitizedConnectionString = builder.ConnectionString;
            }
            catch (ArgumentException ex)
            {
                return (false, Array.Empty<SqlAccountCreationOperation>(), $"Invalid connection string: {ex.Message}");
            }

            await using var connection = new SqlConnection(sanitizedConnectionString);
            await connection.OpenAsync(ct);

            // Step 1: Create the server login
            var loginResult = await CreateLoginAsync(connection, principalName, password, ct);
            operations.Add(loginResult);
            if (!loginResult.Success)
            {
                hasFailures = true;
                // If login creation failed, we can't create users
                return (false, operations, "Failed to create SQL login. Cannot proceed with user creation.");
            }

            // Step 2: Create users in each specified database
            foreach (var database in databases.Distinct())
            {
                if (string.IsNullOrWhiteSpace(database))
                    continue;

                var userResult = await CreateUserAsync(sanitizedConnectionString, principalName, database, ct);
                operations.Add(userResult);
                if (!userResult.Success)
                {
                    hasFailures = true;
                }
            }

            return (!hasFailures, operations, hasFailures ? "Some operations failed. Check individual operation results." : null);
        }
        catch (SqlException ex)
        {
            return (false, operations, $"SQL error: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, operations, $"Unexpected error: {ex.Message}");
        }
    }

    private static async Task<SqlAccountCreationOperation> CreateLoginAsync(
        SqlConnection connection,
        string principalName,
        string password,
        CancellationToken ct)
    {
        try
        {
            // CREATE LOGIN requires a password literal. Keep credentials in parameters
            // on the wire, then quote the identifier and literal inside the SQL batch.
            const string sql = @"
DECLARE @statement nvarchar(max) =
    N'CREATE LOGIN ' + QUOTENAME(@principalName) +
    N' WITH PASSWORD = N''' + REPLACE(@password, N'''', N'''''') + N''';';
EXEC sys.sp_executesql @statement;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@principalName", System.Data.SqlDbType.NVarChar, -1).Value = principalName;
            command.Parameters.Add("@password", System.Data.SqlDbType.NVarChar, -1).Value = password;

            await command.ExecuteNonQueryAsync(ct);

            return new SqlAccountCreationOperation(
                OperationType: "CREATE LOGIN",
                Database: null,
                Success: true,
                ErrorMessage: null);
        }
        catch (SqlException ex)
        {
            return new SqlAccountCreationOperation(
                OperationType: "CREATE LOGIN",
                Database: null,
                Success: false,
                ErrorMessage: ex.Message);
        }
    }

    private static async Task<SqlAccountCreationOperation> CreateUserAsync(
        string connectionString,
        string principalName,
        string database,
        CancellationToken ct)
    {
        try
        {
            // Create a separate connection to the specific database
            // This is safer than using USE statement as it doesn't change the main connection context
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = database
            };
            
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(ct);
            
            var escapedPrincipal = EscapeIdentifier(principalName);
            var sql = $"CREATE USER {escapedPrincipal} FOR LOGIN {escapedPrincipal};";

            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(ct);

            return new SqlAccountCreationOperation(
                OperationType: "CREATE USER",
                Database: database,
                Success: true,
                ErrorMessage: null);
        }
        catch (SqlException ex)
        {
            return new SqlAccountCreationOperation(
                OperationType: "CREATE USER",
                Database: database,
                Success: false,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsSuccessful, IReadOnlyList<string> Databases, string? ErrorMessage)> GetDatabasesAsync(
        Models.SqlIntegration sqlIntegration,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sqlIntegration.ConnectionString))
        {
            return (false, Array.Empty<string>(), "SQL integration has no connection string configured.");
        }

        try
        {
            string sanitizedConnectionString;
            try
            {
                var builder = new SqlConnectionStringBuilder(sqlIntegration.ConnectionString);
                sanitizedConnectionString = builder.ConnectionString;
            }
            catch (ArgumentException ex)
            {
                return (false, Array.Empty<string>(), $"Invalid connection string: {ex.Message}");
            }

            await using var connection = new SqlConnection(sanitizedConnectionString);
            await connection.OpenAsync(ct);

            // Query all online databases, excluding system databases
            // System database IDs: master=1, tempdb=2, model=3, msdb=4
            const string query = @"
                SELECT name 
                FROM sys.databases 
                WHERE state_desc = 'ONLINE' 
                  AND database_id > 4
                ORDER BY name";

            var databases = new List<string>();
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            
            while (await reader.ReadAsync(ct))
            {
                var dbName = reader.GetString(0);
                databases.Add(dbName);
            }

            return (true, databases, null);
        }
        catch (SqlException ex)
        {
            return (false, Array.Empty<string>(), $"SQL error: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, Array.Empty<string>(), $"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsSuccessful, IReadOnlyList<SqlPrincipalPermissions> Principals, string? ErrorMessage)> GetAllPrincipalsAsync(
        Models.SqlIntegration sqlIntegration,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sqlIntegration.ConnectionString))
        {
            return (false, Array.Empty<SqlPrincipalPermissions>(), "SQL integration has no connection string configured.");
        }

        try
        {
            string sanitizedConnectionString;
            try
            {
                var builder = new SqlConnectionStringBuilder(sqlIntegration.ConnectionString);
                sanitizedConnectionString = builder.ConnectionString;
            }
            catch (ArgumentException ex)
            {
                return (false, Array.Empty<SqlPrincipalPermissions>(), $"Invalid connection string: {ex.Message}");
            }

            await using var connection = new SqlConnection(sanitizedConnectionString);
            await connection.OpenAsync(ct);

            // Get all server logins (excluding system accounts)
            const string loginQuery = @"
                SELECT name 
                FROM sys.server_principals 
                WHERE type IN ('S', 'U', 'G', 'E', 'X')
                  AND name NOT LIKE '##%'
                  AND name NOT LIKE 'NT %'
                  AND name NOT IN ('sa', 'public', 'guest')
                ORDER BY name";

            var principalNames = new List<string>();
            await using (var command = new SqlCommand(loginQuery, connection))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var name = reader.GetString(0);
                    principalNames.Add(name);
                }
            }

            var permissions = await ReadPrincipalPermissionsAsync(connection, principalNames, ct);
            return (true, principalNames.Select(name => permissions[name]).ToList(), null);
        }
        catch (SqlException ex)
        {
            return (false, Array.Empty<SqlPrincipalPermissions>(), $"SQL error: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, Array.Empty<SqlPrincipalPermissions>(), $"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsSuccessful, IReadOnlyDictionary<string, SqlPrincipalPermissions> PermissionsMap, string? ErrorMessage)> GetPrincipalPermissionsBatchAsync(
        Models.SqlIntegration sqlIntegration,
        IReadOnlyList<string> principalNames,
        CancellationToken ct = default)
    {
        if (principalNames.Count == 0)
        {
            return (true, new Dictionary<string, SqlPrincipalPermissions>(StringComparer.OrdinalIgnoreCase), null);
        }

        if (string.IsNullOrWhiteSpace(sqlIntegration.ConnectionString))
        {
            return (false, new Dictionary<string, SqlPrincipalPermissions>(), "SQL integration has no connection string configured.");
        }

        // Filter out empty names and deduplicate for efficiency
        var validPrincipals = principalNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validPrincipals.Count == 0)
        {
            return (true, new Dictionary<string, SqlPrincipalPermissions>(StringComparer.OrdinalIgnoreCase), null);
        }

        try
        {
            string sanitizedConnectionString;
            try
            {
                var builder = new SqlConnectionStringBuilder(sqlIntegration.ConnectionString);
                sanitizedConnectionString = builder.ConnectionString;
            }
            catch (ArgumentException ex)
            {
                return (false, new Dictionary<string, SqlPrincipalPermissions>(), $"Invalid connection string: {ex.Message}");
            }

            await using var connection = new SqlConnection(sanitizedConnectionString);
            await connection.OpenAsync(ct);

            var permissions = await ReadPrincipalPermissionsAsync(connection, validPrincipals, ct);
            return (true, permissions, null);
        }
        catch (SqlException ex)
        {
            return (false, new Dictionary<string, SqlPrincipalPermissions>(), $"SQL error: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, new Dictionary<string, SqlPrincipalPermissions>(), $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Escapes a string for use as a literal inside single-quoted SQL string literals.
    /// (Only replaces ' with '' — safe because database names don't contain special characters normally.)
    /// </summary>
    private static string EscapeStringLiteral(string value)
    {
        return value.Replace("'", "''");
    }

}
