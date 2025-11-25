using Fuse.Core.Interfaces;
using Fuse.Core.Models;
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

    private static async Task<bool> CheckPrincipalExistsAsync(SqlConnection connection, string principalName, CancellationToken ct)
    {
        const string query = @"
            SELECT COUNT(*) 
            FROM sys.database_principals 
            WHERE name = @PrincipalName AND type IN ('S', 'U', 'G', 'E', 'X')";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@PrincipalName", principalName);

        var result = await command.ExecuteScalarAsync(ct);
        return result is not null && Convert.ToInt32(result) > 0;
    }

    private static async Task<IReadOnlyList<SqlActualGrant>> GetPrincipalGrantsAsync(SqlConnection connection, string principalName, CancellationToken ct)
    {
        // Query database permissions for the principal
        // This query gets database-level (class=0), object-level (class=1), and schema-level (class=3) permissions
        const string query = @"
            SELECT 
                DB_NAME() AS DatabaseName,
                CASE 
                    WHEN p.class = 0 THEN NULL  -- Database-level permissions
                    WHEN p.class = 3 THEN SCHEMA_NAME(p.major_id)  -- Schema-level permissions
                    ELSE SCHEMA_NAME(o.schema_id)  -- Object-level permissions
                END AS SchemaName,
                p.permission_name AS PermissionName,
                p.state_desc AS StateDesc,
                p.class AS PermissionClass
            FROM sys.database_permissions p
            INNER JOIN sys.database_principals dp ON p.grantee_principal_id = dp.principal_id
            LEFT JOIN sys.objects o ON p.major_id = o.object_id AND p.class = 1
            WHERE dp.name = @PrincipalName
              AND p.state_desc IN ('GRANT', 'GRANT_WITH_GRANT_OPTION')
              AND p.class IN (0, 1, 3)
            ORDER BY DatabaseName, SchemaName, PermissionName";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@PrincipalName", principalName);

        var grants = new Dictionary<(string?, string?), HashSet<Privilege>>();

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var databaseName = reader.IsDBNull(0) ? null : reader.GetString(0);
            var schemaName = reader.IsDBNull(1) ? null : reader.GetString(1);
            var permissionName = reader.GetString(2);

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
}
