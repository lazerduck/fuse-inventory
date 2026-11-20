using Fuse.Core.Models;
using Microsoft.Data.SqlClient;

namespace Fuse.Core.Areas.Account;

public partial class AccountSqlInspector
{
    // Single-account, batch and orphan inspection use the same query so their scope
    // and inherited-permission semantics cannot diverge.
    private static async Task<IReadOnlyDictionary<string, SqlPrincipalPermissions>> ReadPrincipalPermissionsAsync(
        SqlConnection connection, IReadOnlyList<string> principalNames, CancellationToken ct, IReadOnlyList<string>? databases = null)
    {
        const string createPrincipals = "CREATE TABLE #Principals (Name NVARCHAR(128) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY); CREATE TABLE #RequestedDatabases (Name NVARCHAR(128) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);";
        await using (var command = new SqlCommand(createPrincipals, connection))
            await command.ExecuteNonQueryAsync(ct);

        // Keep names parameterized and below SQL Server's parameter limit. Dynamic
        // SQL below stays constant in size regardless of the number of principals.
        for (var offset = 0; offset < principalNames.Count; offset += 500)
        {
            var batch = principalNames.Skip(offset).Take(500).ToList();
            var values = string.Join(", ", Enumerable.Range(0, batch.Count).Select(i => $"(@n{i})"));
            await using var insert = new SqlCommand($"INSERT INTO #Principals (Name) VALUES {values}", connection);
            for (var index = 0; index < batch.Count; index++)
                insert.Parameters.AddWithValue($"@n{index}", batch[index]);
            await insert.ExecuteNonQueryAsync(ct);
        }

        // A new user inherits public only in the databases where a user is created.
        if (databases is not null)
        {
            foreach (var names in databases.Chunk(500))
            {
                var values = string.Join(", ", names.Select((_, i) => $"(@d{i})"));
                await using var insert = new SqlCommand($"INSERT INTO #RequestedDatabases (Name) VALUES {values}", connection);
                for (var index = 0; index < names.Length; index++) insert.Parameters.AddWithValue($"@d{index}", names[index]);
                await insert.ExecuteNonQueryAsync(ct);
            }
        }
        const string query = """
            SET NOCOUNT ON;
            CREATE TABLE #PermissionRows (
                PrincipalName NVARCHAR(128) COLLATE DATABASE_DEFAULT,
                DatabaseName NVARCHAR(128) COLLATE DATABASE_DEFAULT,
                SchemaName NVARCHAR(128) COLLATE DATABASE_DEFAULT,
                ObjectName NVARCHAR(128) COLLATE DATABASE_DEFAULT,
                ColumnName NVARCHAR(128) COLLATE DATABASE_DEFAULT,
                PermissionName NVARCHAR(128) COLLATE DATABASE_DEFAULT,
                PermissionState CHAR(1), PermissionClass INT, Inherited BIT
            );
            IF @limited=1 AND EXISTS(SELECT 1 FROM #RequestedDatabases WHERE DB_ID(Name) IS NULL OR ISNULL(HAS_DBACCESS(Name),0) <> 1)
                THROW 50001, 'One or more requested databases are unavailable.', 1;
            -- Temp names use the initial database's collation. Resolve server-level names
            -- to catalog IDs before comparing them with server metadata.
            DECLARE @Database SYSNAME, @Sql NVARCHAR(MAX);
            DECLARE databases CURSOR LOCAL FAST_FORWARD FOR
                SELECT name FROM sys.databases WHERE state_desc = 'ONLINE' AND HAS_DBACCESS(name) = 1
                AND (@limited=0 OR database_id IN (SELECT DB_ID(Name) FROM #RequestedDatabases));
            OPEN databases;
            FETCH NEXT FROM databases INTO @Database;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                SET @Sql = N'USE ' + QUOTENAME(@Database) + N';
                    CREATE TABLE #RoleClosure (
                        PrincipalName NVARCHAR(128) COLLATE DATABASE_DEFAULT,
                        MemberId INT,
                        PRIMARY KEY (PrincipalName, MemberId)
                    );
                    INSERT INTO #RoleClosure
                    SELECT pr.Name, dp.principal_id FROM #Principals pr
                    JOIN sys.database_principals dp ON dp.name COLLATE DATABASE_DEFAULT = pr.Name COLLATE DATABASE_DEFAULT;

                    -- All database users inherit public. Its user-defined grants matter.
                    INSERT INTO #RoleClosure
                    SELECT DISTINCT c.PrincipalName, r.principal_id FROM #RoleClosure c
                    CROSS JOIN sys.database_principals r
                    WHERE r.name = ''public'' AND NOT EXISTS (
                        SELECT 1 FROM #RoleClosure seen
                        WHERE seen.PrincipalName = c.PrincipalName AND seen.MemberId = r.principal_id);

                    -- Iterative closure handles nested/diamond memberships without
                    -- duplicate grants, recursive depth limits or cycles.
                    WHILE 1 = 1
                    BEGIN
                        INSERT INTO #RoleClosure
                        SELECT DISTINCT c.PrincipalName, rm.role_principal_id
                        FROM #RoleClosure c
                        JOIN sys.database_role_members rm ON rm.member_principal_id = c.MemberId
                        WHERE NOT EXISTS (SELECT 1 FROM #RoleClosure seen
                            WHERE seen.PrincipalName = c.PrincipalName AND seen.MemberId = rm.role_principal_id);
                        IF @@ROWCOUNT = 0 BREAK;
                    END;

                    INSERT INTO #PermissionRows
                    SELECT c.PrincipalName, DB_NAME(), s.name, o.name, col.name,
                           p.permission_name, p.state, p.class, CASE WHEN grantee.name=c.PrincipalName THEN 0 ELSE 1 END
                    FROM #RoleClosure c
                    JOIN sys.database_permissions p ON p.grantee_principal_id = c.MemberId
                    JOIN sys.database_principals grantee ON grantee.principal_id = c.MemberId
                    LEFT JOIN sys.all_objects o ON p.class = 1 AND p.major_id = o.object_id
                    LEFT JOIN sys.schemas s ON s.schema_id = CASE
                        WHEN p.class = 3 THEN p.major_id WHEN p.class = 1 THEN o.schema_id END
                    LEFT JOIN sys.columns col ON p.class = 1 AND p.major_id = col.object_id AND p.minor_id = col.column_id
                    WHERE p.state IN (''G'', ''W'', ''D'')
                      -- Built-in public metadata visibility is not an application grant.
                      -- Negative object IDs identify SQL Server system objects, including
                      -- metadata objects hidden from sys.all_objects on some versions.
                      AND NOT (grantee.name = ''public'' AND p.class = 1 AND p.major_id < 0 AND p.state IN (''G'', ''W''))
                    UNION ALL
                    SELECT c.PrincipalName, DB_NAME(), NULL, NULL, NULL,
                           perms.PermissionName, perms.PermissionState, 0, 1
                    FROM #RoleClosure c
                    JOIN sys.database_principals r ON r.principal_id = c.MemberId AND r.is_fixed_role = 1
                    CROSS APPLY (SELECT PermissionName, PermissionState FROM (VALUES
                        (''db_datareader'', ''SELECT'', ''G''),
                        (''db_datawriter'', ''INSERT'', ''G''),
                        (''db_datawriter'', ''UPDATE'', ''G''),
                        (''db_datawriter'', ''DELETE'', ''G''),
                        (''db_owner'', ''SELECT'', ''G''),
                        (''db_owner'', ''INSERT'', ''G''),
                        (''db_owner'', ''UPDATE'', ''G''),
                        (''db_owner'', ''DELETE'', ''G''),
                        (''db_owner'', ''EXECUTE'', ''G''),
                        (''db_owner'', ''ALTER'', ''G''),
                        (''db_owner'', ''CONTROL'', ''G''),
                        (''db_ddladmin'', ''ALTER'', ''G''),
                        (''db_denydatareader'', ''SELECT'', ''D''),
                        (''db_denydatawriter'', ''INSERT'', ''D''),
                        (''db_denydatawriter'', ''UPDATE'', ''D''),
                        (''db_denydatawriter'', ''DELETE'', ''D'')
                    ) AS RolePerms(RoleName, PermissionName, PermissionState) WHERE RolePerms.RoleName = r.name) perms;
                    DROP TABLE #RoleClosure;';
                EXEC sys.sp_executesql @Sql;
                FETCH NEXT FROM databases INTO @Database;
            END;
            CLOSE databases;
            DEALLOCATE databases;
            SELECT PrincipalName, DatabaseName, SchemaName, ObjectName, ColumnName,
                   PermissionName, PermissionState, PermissionClass, Inherited FROM #PermissionRows;
            SELECT pr.Name FROM #Principals pr
            JOIN sys.server_principals sp ON sp.principal_id = SUSER_ID(pr.Name)
            WHERE sp.type IN ('S', 'U', 'G', 'E', 'X');
            """;

        var grants = principalNames.ToDictionary(name => name,
            _ => new Dictionary<(string Database, string? Schema), HashSet<Privilege>>(), StringComparer.OrdinalIgnoreCase);
        var inherited = principalNames.ToDictionary(name => name,
            _ => new Dictionary<(string Database, string? Schema), HashSet<Privilege>>(), StringComparer.OrdinalIgnoreCase);
        var errors = principalNames.ToDictionary(name => name,
            _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var inspectionCommand = new SqlCommand(query, connection);
        inspectionCommand.Parameters.Add("@limited", System.Data.SqlDbType.Bit).Value = databases is not null;
        await using (var reader = await inspectionCommand.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var principal = reader.GetString(0);
                var database = reader.GetString(1);
                var schema = reader.IsDBNull(2) ? null : reader.GetString(2);
                var objectName = reader.IsDBNull(3) ? null : reader.GetString(3);
                var columnName = reader.IsDBNull(4) ? null : reader.GetString(4);
                var permission = reader.GetString(5);
                var state = reader.GetString(6);
                var permissionClass = reader.GetInt32(7);
                var scope = EscapeIdentifier(database)
                    + (schema is null ? "" : "." + EscapeIdentifier(schema))
                    + (objectName is null ? "" : "." + EscapeIdentifier(objectName))
                    + (columnName is null ? "" : "." + EscapeIdentifier(columnName));

                if (state == "D")
                {
                    errors[principal].Add($"DENY {permission} on {scope}");
                    continue;
                }
                if (permissionClass is not (0 or 3))
                {
                    var kind = permissionClass == 1 ? "object/column" : $"securable class {permissionClass}";
                    errors[principal].Add($"{kind} {permission} on {scope}");
                    continue;
                }
                var privilege = MapSqlPermissionToPrivilege(permission);
                if (!privilege.HasValue)
                    continue;
                var key = (database, schema);
                if (!grants[principal].TryGetValue(key, out var privileges))
                    grants[principal][key] = privileges = new HashSet<Privilege>();
                privileges.Add(privilege.Value);
                if (reader.GetBoolean(8))
                {
                    if (!inherited[principal].TryGetValue(key, out var fromRoles)) inherited[principal][key] = fromRoles = [];
                    fromRoles.Add(privilege.Value);
                }
            }
            await reader.NextResultAsync(ct);
            while (await reader.ReadAsync(ct))
                existing.Add(reader.GetString(0));
        }

        return principalNames.ToDictionary(name => name, name => new SqlPrincipalPermissions(
            name, existing.Contains(name),
            grants[name].Select(grant => new SqlActualGrant(grant.Key.Database, grant.Key.Schema, grant.Value)
                { Inherited = inherited[name].GetValueOrDefault(grant.Key) ?? [] }).ToList(),
            errors[name].Count == 0 ? null : "Unsupported SQL permissions cannot be represented safely by database/schema grants: "
                + string.Join("; ", errors[name].Order(StringComparer.Ordinal))
                + ". Permission comparison, import and resolution are disabled for this principal."), StringComparer.OrdinalIgnoreCase);
    }
}
