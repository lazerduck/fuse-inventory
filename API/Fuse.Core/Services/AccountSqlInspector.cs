using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Microsoft.Data.SqlClient;

namespace Fuse.Core.Services;

/// <summary>
/// Inspects SQL Server principals and their permissions.
/// </summary>
public class AccountSqlInspector : IAccountSqlInspector
{
    /// <inheritdoc />
    public async Task<(bool IsSuccessful, SqlPrincipalPermissions? Permissions, string? ErrorMessage)> GetPrincipalPermissionsAsync(
        SqlIntegration sqlIntegration,
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

            // Check if principal exists
            bool principalExists = await CheckPrincipalExistsAsync(connection, principalName, ct);
            if (!principalExists)
            {
                return (true, new SqlPrincipalPermissions(principalName, false, Array.Empty<SqlActualGrant>()), null);
            }

            // Get the actual permissions for the principal
            var grants = await GetPrincipalGrantsAsync(connection, principalName, ct);

            return (true, new SqlPrincipalPermissions(principalName, true, grants), null);
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
        SqlIntegration sqlIntegration,
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

    private static async Task<bool> CheckPrincipalExistsAsync(SqlConnection connection, string principalName, CancellationToken ct)
    {
        // Check if login exists at server level (not just user in current database)
        const string query = @"
            SELECT COUNT(*) 
            FROM sys.server_principals 
            WHERE name = @PrincipalName AND type IN ('S', 'U', 'G', 'E', 'X')";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@PrincipalName", principalName);

        var result = await command.ExecuteScalarAsync(ct);
        return result is not null && Convert.ToInt32(result) > 0;
    }

    private static async Task<IReadOnlyList<SqlActualGrant>> GetPrincipalGrantsAsync(SqlConnection connection, string principalName, CancellationToken ct)
    {
        // Query to check permissions across all accessible databases
        const string query = @"
            DECLARE @PrincipalName NVARCHAR(128) = @PrincipalNameParam;
            DECLARE @SQL NVARCHAR(MAX) = '';

            -- Build dynamic SQL to query each database using fully qualified names
            SELECT @SQL = @SQL + 
                'SELECT 
                    ''' + name + ''' AS DatabaseName,
                    CASE 
                        WHEN p.class = 0 THEN NULL
                        WHEN p.class = 3 THEN SCHEMA_NAME(p.major_id)
                        ELSE SCHEMA_NAME(o.schema_id)
                    END AS SchemaName,
                    p.permission_name AS PermissionName
                FROM ' + QUOTENAME(name) + '.sys.database_permissions p
                INNER JOIN ' + QUOTENAME(name) + '.sys.database_principals dp ON p.grantee_principal_id = dp.principal_id
                LEFT JOIN ' + QUOTENAME(name) + '.sys.objects o ON p.major_id = o.object_id AND p.class = 1
                WHERE dp.name = @PrincipalName
                  AND p.state_desc IN (''GRANT'', ''GRANT_WITH_GRANT_OPTION'')
                  AND p.class IN (0, 1, 3)
                UNION ALL
                SELECT 
                    ''' + name + ''' AS DatabaseName,
                    NULL AS SchemaName,
                    perms.PermissionName
                FROM ' + QUOTENAME(name) + '.sys.database_role_members rm
                INNER JOIN ' + QUOTENAME(name) + '.sys.database_principals dp ON rm.member_principal_id = dp.principal_id
                INNER JOIN ' + QUOTENAME(name) + '.sys.database_principals r ON rm.role_principal_id = r.principal_id
                CROSS APPLY (
                    SELECT PermissionName FROM (VALUES
                        (''db_datareader'', ''SELECT''),
                        (''db_datawriter'', ''INSERT''),
                        (''db_datawriter'', ''UPDATE''),
                        (''db_datawriter'', ''DELETE''),
                        (''db_owner'', ''CONTROL''),
                        (''db_ddladmin'', ''ALTER''),
                        (''db_executor'', ''EXECUTE'')
                    ) AS RolePerms(RoleName, PermissionName)
                    WHERE RolePerms.RoleName = r.name
                ) perms
                WHERE dp.name = @PrincipalName
                  AND r.name IN (''db_datareader'', ''db_datawriter'', ''db_owner'', ''db_ddladmin'', ''db_executor'')
                UNION ALL '
            FROM sys.databases
            WHERE state_desc = 'ONLINE' 
              AND (database_id > 4 OR name IN ('master', 'msdb', 'model', 'tempdb'));

            -- Remove trailing UNION ALL and execute
            IF LEN(@SQL) > 0
            BEGIN
                SET @SQL = LEFT(@SQL, LEN(@SQL) - 10);  -- Remove last 'UNION ALL'
                EXEC sp_executesql @SQL, N'@PrincipalName NVARCHAR(128)', @PrincipalName;
            END";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@PrincipalNameParam", principalName);

        var grants = new Dictionary<(string?, string?), HashSet<Privilege>>();

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var databaseName = reader.IsDBNull(0) ? null : reader.GetString(0);
            var schemaName = reader.IsDBNull(1) ? null : reader.GetString(1);
            var permissionName = reader.IsDBNull(2) ? null : reader.GetString(2);

            if (string.IsNullOrWhiteSpace(permissionName))
                continue;

            var key = (databaseName, schemaName);
            if (!grants.TryGetValue(key, out var privileges))
            {
                privileges = new HashSet<Privilege>();
                grants[key] = privileges;
            }

            // Map SQL Server permissions to Fuse Privilege enum
            var privilege = MapSqlPermissionToPrivilege(permissionName);
            if (privilege.HasValue)
            {
                privileges.Add(privilege.Value);
            }
        }

        return grants.Select(g => new SqlActualGrant(g.Key.Item1, g.Key.Item2, g.Value)).ToList();
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
        SqlIntegration sqlIntegration,
        string principalName,
        string password,
        IReadOnlyList<string> databases,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(principalName))
        {
            return (false, Array.Empty<SqlAccountCreationOperation>(), "Principal name is required.");
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
            // Use sp_executesql with parameterized password to avoid SQL injection.
            // The login name must be an identifier (escaped with brackets), but the password
            // can be safely passed as a parameter through dynamic SQL execution.
            var escapedPrincipal = EscapeIdentifier(principalName);
            
            // Build the dynamic SQL that will be executed by sp_executesql
            // The @password parameter is safely passed to the dynamic SQL
            var dynamicSql = $"CREATE LOGIN {escapedPrincipal} WITH PASSWORD = @password;";

            await using var command = new SqlCommand("sp_executesql", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@stmt", dynamicSql);
            command.Parameters.AddWithValue("@params", "@password NVARCHAR(MAX)");
            command.Parameters.AddWithValue("@password", password);
            
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
}
