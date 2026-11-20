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

        if (permissionComparisons.Any(c => !Helpers.SqlPermissionDiff.IsDatabaseScope(c.Database, c.Schema)))
            return (false, [], "SQL permission changes require a whole database. Schema, object and column permissions are inspection only.");

        var operations = new List<DriftResolutionOperation>();
        try
        {
            await using var connection = new SqlConnection(sqlIntegration.ConnectionString);
            await connection.OpenAsync(ct);
            var invalid = await FindInvalidDatabaseAsync(connection, permissionComparisons.Select(c => c.Database!), ct);
            if (invalid != null) return (false, [], Helpers.SqlPermissionDiff.DatabaseNameWarning(invalid));
            foreach (var comparison in permissionComparisons)
            {
                foreach (var privilege in comparison.MissingPrivileges.Except(comparison.BlockedPrivileges))
                    operations.Add(await ApplyDatabasePermissionAsync(connection, principalName, comparison.Database!, privilege, true, ct));
                foreach (var privilege in comparison.ExtraPrivileges.Except(comparison.BlockedPrivileges))
                    operations.Add(await ApplyDatabasePermissionAsync(connection, principalName, comparison.Database!, privilege, false, ct));
            }

            var blocked = permissionComparisons.Any(c => c.BlockedPrivileges.Count > 0);
            var success = !blocked && operations.All(o => o.Success);
            return (success, operations, blocked ? "Some database privileges require manual review of a role, DENY or grant option."
                : success ? null : "Some database operations failed. Check individual results.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, operations, $"Could not apply database privileges: {ex.Message}");
        }
    }

    private static async Task<string?> FindInvalidDatabaseAsync(SqlConnection connection, IEnumerable<string> databases, CancellationToken ct)
    {
        var requested = databases.Distinct(StringComparer.Ordinal).ToArray();
        if (requested.Length == 0) return null;
        var catalog = new HashSet<string>(StringComparer.Ordinal);
        await using var command = new SqlCommand("SELECT name FROM sys.databases", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) catalog.Add(reader.GetString(0));
        return requested.FirstOrDefault(name => !catalog.Contains(name));
    }

    private static async Task<DriftResolutionOperation> ApplyDatabasePermissionAsync(
        SqlConnection connection, string principalName, string database, Privilege privilege, bool grant, CancellationToken ct)
    {
        var operation = grant ? "GRANT" : "REVOKE";
        var permission = MapPrivilegeToSqlPermission(privilege);
        if (permission is null)
            return new(operation, database, null, privilege, false, $"Unsupported database privilege '{privilege}'.");
        // Only mapped permission keywords and quoted identifiers enter DDL. A database
        // GRANT must never replace an existing DENY or alter a delegated grant.
        var sql = $"""
            USE {EscapeIdentifier(database)};
            IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @principal AND sid = SUSER_SID(@principal))
                THROW 50001, 'The database user does not match this server login.', 1;
            IF EXISTS (SELECT 1 FROM sys.database_permissions WHERE class = 0 AND grantee_principal_id = USER_ID(@principal)
                        AND permission_name = @permission AND state IN ('D','W'))
                THROW 50001, 'A database DENY or grant option requires manual review.', 1;
            {operation} {permission} {(grant ? "TO" : "FROM")} {EscapeIdentifier(principalName)};
            """;
        try
        {
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@principal", principalName);
            command.Parameters.AddWithValue("@permission", permission);
            await command.ExecuteNonQueryAsync(ct);
            return new(operation, database, null, privilege, true, null);
        }
        catch (SqlException ex)
        {
            return new(operation, database, null, privilege, false, ex.Message);
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
            // SQL metadata and administrative permissions do not confer ALTER or SELECT on application data.
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

            var invalid = await FindInvalidDatabaseAsync(connection, databases, ct);
            if (invalid != null) return (false, [], Helpers.SqlPermissionDiff.DatabaseNameWarning(invalid));

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
            return (true, new Dictionary<string, SqlPrincipalPermissions>(StringComparer.Ordinal), null);
        }

        if (string.IsNullOrWhiteSpace(sqlIntegration.ConnectionString))
        {
            return (false, new Dictionary<string, SqlPrincipalPermissions>(), "SQL integration has no connection string configured.");
        }

        // Filter out empty names and deduplicate for efficiency
        var validPrincipals = principalNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (validPrincipals.Count == 0)
        {
            return (true, new Dictionary<string, SqlPrincipalPermissions>(StringComparer.Ordinal), null);
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
