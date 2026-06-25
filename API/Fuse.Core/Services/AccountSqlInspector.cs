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
    public async Task<(bool IsSuccessful, IReadOnlyList<string> PrincipalNames, string? ErrorMessage)> GetAllPrincipalNamesAsync(
        SqlIntegration sqlIntegration,
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
                        (''db_owner'', ''SELECT''),
                        (''db_owner'', ''INSERT''),
                        (''db_owner'', ''UPDATE''),
                        (''db_owner'', ''DELETE''),
                        (''db_owner'', ''EXECUTE''),
                        (''db_owner'', ''ALTER''),
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

    /// <inheritdoc />
    public async Task<(bool IsSuccessful, IReadOnlyList<string> Databases, string? ErrorMessage)> GetDatabasesAsync(
        SqlIntegration sqlIntegration,
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
        SqlIntegration sqlIntegration,
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

            // For each principal, get their permissions
            var principals = new List<SqlPrincipalPermissions>();
            foreach (var principalName in principalNames)
            {
                var grants = await GetPrincipalGrantsAsync(connection, principalName, ct);
                principals.Add(new SqlPrincipalPermissions(principalName, true, grants));
            }

            return (true, principals, null);
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
        SqlIntegration sqlIntegration,
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

            // Load the principal names into a temporary table so the server-side dynamic
            // SQL can JOIN against them. This avoids embedding the names in the query text
            // (as an IN clause) or in the dynamically-built UNION ALL string, keeping the
            // size of @SQL proportional only to the number of databases — not to the
            // number of principals. Sending a very large pre-assembled UNION ALL query
            // from C# causes SQL Server to allocate a Large Object in tempdb for the query
            // text, which can exhaust tempdb space.
            //
            // COLLATE DATABASE_DEFAULT ensures the column uses the current connection's
            // database collation rather than tempdb's collation (which may differ on
            // servers configured with a non-default collation). The JOIN in the dynamic
            // SQL also collates dp.name to DATABASE_DEFAULT so both sides always match,
            // regardless of the individual user-database collation.
            const string createTempTable = "CREATE TABLE #Principals (Name NVARCHAR(128) COLLATE DATABASE_DEFAULT NOT NULL);";
            await using (var cmd = new SqlCommand(createTempTable, connection))
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Insert all principal names in batches to minimise round-trips while staying
            // within SQL Server's 2100-parameter limit.
            const int insertBatchSize = 500;
            for (int offset = 0; offset < validPrincipals.Count; offset += insertBatchSize)
            {
                var batch = validPrincipals.Skip(offset).Take(insertBatchSize).ToList();
                var paramPlaceholders = string.Join(", ", Enumerable.Range(0, batch.Count).Select(j => $"(@n{j})"));
                await using var insertCmd = new SqlCommand(
                    $"INSERT INTO #Principals (Name) VALUES {paramPlaceholders}", connection);
                for (int j = 0; j < batch.Count; j++)
                    insertCmd.Parameters.AddWithValue($"@n{j}", batch[j]);
                await insertCmd.ExecuteNonQueryAsync(ct);
            }

            var validPrincipalsSet = new HashSet<string>(validPrincipals, StringComparer.OrdinalIgnoreCase);

            // Build the cross-database UNION ALL on the SQL Server side using T-SQL string
            // concatenation — the same pattern used in GetPrincipalGrantsAsync. The C# code
            // sends only a small, fixed-size script; SQL Server builds and executes @SQL
            // internally. The #Principals temp table (session-scoped) is accessible from
            // within sp_executesql and is used for filtering so the dynamically-built SQL
            // stays proportional to the number of databases only.
            const string dynamicSqlQuery = @"
                DECLARE @SQL NVARCHAR(MAX) = N'';

                SELECT @SQL = @SQL +
                    'SELECT dp.name COLLATE DATABASE_DEFAULT AS PrincipalName, ''' + REPLACE(name, '''', '''''') + ''' AS DatabaseName, '
                    + 'CASE WHEN p.class = 0 THEN CAST(NULL AS NVARCHAR(128)) '
                    + 'WHEN p.class = 3 THEN SCHEMA_NAME(p.major_id) '
                    + 'ELSE SCHEMA_NAME(o.schema_id) END AS SchemaName, '
                    + 'p.permission_name AS PermissionName '
                    + 'FROM ' + QUOTENAME(name) + '.sys.database_permissions p '
                    + 'INNER JOIN ' + QUOTENAME(name) + '.sys.database_principals dp ON p.grantee_principal_id = dp.principal_id '
                    + 'LEFT JOIN ' + QUOTENAME(name) + '.sys.objects o ON p.major_id = o.object_id AND p.class = 1 '
                    + 'INNER JOIN #Principals pr ON dp.name COLLATE DATABASE_DEFAULT = pr.Name '
                    + 'WHERE p.state_desc IN (''GRANT'', ''GRANT_WITH_GRANT_OPTION'') AND p.class IN (0, 1, 3) '
                    + 'UNION ALL '
                    + 'SELECT dp.name COLLATE DATABASE_DEFAULT, ''' + REPLACE(name, '''', '''''') + ''', NULL AS SchemaName, perms.PermissionName '
                    + 'FROM ' + QUOTENAME(name) + '.sys.database_role_members rm '
                    + 'INNER JOIN ' + QUOTENAME(name) + '.sys.database_principals dp ON rm.member_principal_id = dp.principal_id '
                    + 'INNER JOIN ' + QUOTENAME(name) + '.sys.database_principals r ON rm.role_principal_id = r.principal_id '
                    + 'INNER JOIN #Principals pr ON dp.name COLLATE DATABASE_DEFAULT = pr.Name '
                    + 'CROSS APPLY (SELECT PermissionName FROM (VALUES '
                    + '(''db_datareader'', ''SELECT''), '
                    + '(''db_datawriter'', ''INSERT''), '
                    + '(''db_datawriter'', ''UPDATE''), '
                    + '(''db_datawriter'', ''DELETE''), '
                    + '(''db_owner'', ''SELECT''), '
                    + '(''db_owner'', ''INSERT''), '
                    + '(''db_owner'', ''UPDATE''), '
                    + '(''db_owner'', ''DELETE''), '
                    + '(''db_owner'', ''EXECUTE''), '
                    + '(''db_owner'', ''ALTER''), '
                    + '(''db_owner'', ''CONTROL''), '
                    + '(''db_ddladmin'', ''ALTER''), '
                    + '(''db_executor'', ''EXECUTE'') '
                    + ') AS RolePerms(RoleName, PermissionName) WHERE RolePerms.RoleName = r.name) perms '
                    + 'WHERE r.name IN (''db_datareader'', ''db_datawriter'', ''db_owner'', ''db_ddladmin'', ''db_executor'') '
                    + 'UNION ALL '
                FROM sys.databases
                WHERE state_desc = 'ONLINE'
                  AND (database_id > 4 OR name IN ('master', 'msdb', 'model', 'tempdb'));

                IF LEN(@SQL) > 0
                BEGIN
                    SET @SQL = LEFT(@SQL, LEN(@SQL) - 10);
                    EXEC sp_executesql @SQL;
                END";

            var allRows = new List<(string PrincipalName, string? DatabaseName, string? SchemaName, string PermissionName)>();

            await using (var command = new SqlCommand(dynamicSqlQuery, connection))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var principalName = reader.IsDBNull(0) ? null : reader.GetString(0);
                    var databaseName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var schemaName = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var permissionName = reader.IsDBNull(3) ? null : reader.GetString(3);

                    if (string.IsNullOrWhiteSpace(permissionName))
                        continue;

                    if (!string.IsNullOrWhiteSpace(principalName))
                    {
                        allRows.Add((principalName, databaseName, schemaName, permissionName));
                    }
                }
            }

            // Group by principal name and build the result dictionary,
            // restricted to only the requested principals.
            var resultMap = new Dictionary<string, SqlPrincipalPermissions>(StringComparer.OrdinalIgnoreCase);

            var grouped = allRows
                .Where(r => !string.IsNullOrWhiteSpace(r.PrincipalName) && validPrincipalsSet.Contains(r.PrincipalName!))
                .GroupBy(r => r.PrincipalName!, StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouped)
            {
                var principalName = group.Key;

                var grantsMap = new Dictionary<(string?, string?), HashSet<Privilege>>();

                foreach (var row in group)
                {
                    if (string.IsNullOrWhiteSpace(row.PermissionName))
                        continue;

                    var key = (row.DatabaseName, row.SchemaName);
                    if (!grantsMap.TryGetValue(key, out var privileges))
                    {
                        privileges = new HashSet<Privilege>();
                        grantsMap[key] = privileges;
                    }

                    var privilege = MapSqlPermissionToPrivilege(row.PermissionName);
                    if (privilege.HasValue)
                    {
                        privileges.Add(privilege.Value);
                    }
                }

                var grants = grantsMap.Select(g => new SqlActualGrant(g.Key.Item1, g.Key.Item2, g.Value)).ToList();
                resultMap[principalName] = new SqlPrincipalPermissions(principalName, true, grants);
            }

            // Principals not found in the permissions rows may still exist at the server level
            // (e.g. a login with no grants). Check existence to correctly set Exists=true/false.
            var missingPrincipals = validPrincipals.Where(n => !resultMap.ContainsKey(n)).ToList();
            if (missingPrincipals.Count > 0)
            {
                var missingInClause = "(" + string.Join(", ", missingPrincipals.Select(n => "N'" + EscapeStringLiteral(n) + "'")) + ")";
                var existingSet = await GetExistingServerPrincipalsAsync(connection, missingPrincipals, missingInClause, ct);
                foreach (var name in missingPrincipals)
                {
                    resultMap[name] = new SqlPrincipalPermissions(name, existingSet.Contains(name), Array.Empty<SqlActualGrant>());
                }
            }

            return (true, resultMap, null);
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

    /// <summary>
    /// Returns the subset of <paramref name="principalNames"/> that exist as server-level logins.
    /// </summary>
    private static async Task<HashSet<string>> GetExistingServerPrincipalsAsync(
        SqlConnection connection,
        IReadOnlyList<string> principalNames,
        string principalInClause,
        CancellationToken ct)
    {
        var query = $"SELECT name FROM sys.server_principals WHERE name IN {principalInClause} AND type IN ('S', 'U', 'G', 'E', 'X')";

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new SqlCommand(query, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                existing.Add(reader.GetString(0));
            }
        }
        return existing;
    }
}
