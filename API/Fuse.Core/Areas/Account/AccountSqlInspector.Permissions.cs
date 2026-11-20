using Fuse.Core.Models;
using Fuse.Core.Responses;
using Microsoft.Data.SqlClient;

namespace Fuse.Core.Areas.Account;

public partial class AccountSqlInspector
{
    // Single-account, batch and orphan inspection use the same query so their scope
    // and inherited-permission semantics cannot diverge.
    private static async Task<IReadOnlyDictionary<string, SqlPrincipalPermissions>> ReadPrincipalPermissionsAsync(
        SqlConnection connection, IReadOnlyList<string> principalNames, CancellationToken ct, IReadOnlyList<string>? databases = null)
    {
        const string createPrincipals = """
            CREATE TABLE #Principals (RequestId INT IDENTITY PRIMARY KEY, Name NVARCHAR(128) COLLATE DATABASE_DEFAULT NOT NULL, LoginSid VARBINARY(85) NULL, LoginName SYSNAME NULL);
            CREATE TABLE #RequestedDatabases (Name NVARCHAR(128) COLLATE DATABASE_DEFAULT NOT NULL);
            """;
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
            UPDATE pr SET LoginSid = sp.sid, LoginName=sp.name FROM #Principals pr
            JOIN sys.server_principals sp ON sp.principal_id = SUSER_ID(pr.Name)
            WHERE sp.type IN ('S','U','G','E','X');
            CREATE TABLE #PermissionRows (
                RequestId INT, DatabaseName SYSNAME, SchemaName SYSNAME NULL, ObjectName SYSNAME NULL, ColumnName SYSNAME NULL,
                PermissionName SYSNAME, PermissionState SYSNAME, ScopeName SYSNAME, Grantee SYSNAME, Grantor SYSNAME NULL,
                Inherited BIT, MajorId INT, MinorId INT
            );
            CREATE TABLE #ExecTargets (DatabaseName SYSNAME, SchemaName SYSNAME, ObjectName SYSNAME, ObjectType VARCHAR(2));
            CREATE TABLE #DataColumns (DatabaseName SYSNAME, SchemaName SYSNAME, ObjectName SYSNAME, ColumnName SYSNAME);
            CREATE TABLE #Gaps (DatabaseName SYSNAME NULL, RequestId INT NULL, Reason NVARCHAR(200));
            IF @limited=0 AND (ISNULL(HAS_PERMS_BY_NAME(NULL,NULL,'VIEW ANY DATABASE'),0) <> 1
                OR ISNULL(HAS_PERMS_BY_NAME(NULL,NULL,'VIEW ANY DEFINITION'),0) <> 1)
                INSERT INTO #Gaps VALUES (NULL,NULL,'Server catalog visibility is limited.');
            INSERT INTO #Gaps SELECT d.name, NULL, N'Database is offline or inaccessible.'
                FROM sys.databases d WHERE d.database_id > 4 AND (d.state<>0 OR ISNULL(HAS_DBACCESS(d.name),0)<>1)
                AND (@limited=0 OR d.database_id IN (SELECT DB_ID(Name) FROM #RequestedDatabases));
            -- These principals can have access outside the database user and role catalog.
            INSERT INTO #Gaps SELECT NULL, pr.RequestId, N'Server permissions or group membership may supply additional access.'
                FROM #Principals pr JOIN sys.server_principals sp ON sp.sid=pr.LoginSid
                WHERE sp.type IN ('U','G','E','X') OR EXISTS (
                    SELECT 1 FROM sys.server_role_members m WHERE m.member_principal_id=sp.principal_id)
                    OR EXISTS (SELECT 1 FROM sys.server_permissions p WHERE p.grantee_principal_id=sp.principal_id
                        AND p.permission_name IN ('CONTROL SERVER','SELECT ALL USER SECURABLES','CONNECT ANY DATABASE'));
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
                    CREATE TABLE #RoleClosure (RequestId INT, UserId INT, MemberId INT, PRIMARY KEY (RequestId, MemberId));
                    INSERT INTO #RoleClosure
                    SELECT pr.RequestId, dp.principal_id, dp.principal_id FROM #Principals pr
                    JOIN sys.database_principals dp ON
                        (@baseline=0 AND dp.sid=pr.LoginSid AND dp.type IN (''S'',''U'',''G'',''E'',''X''))
                        OR (@baseline=1 AND dp.principal_id=DATABASE_PRINCIPAL_ID(''public''));

                    IF (DB_ID()>4 OR EXISTS(SELECT 1 FROM #RoleClosure)) AND (
                        ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),''DATABASE'',''VIEW DEFINITION''),0) <> 1
                        OR (USER_ID()<>1 AND EXISTS (SELECT 1 FROM sys.database_permissions hidden
                            JOIN sys.user_token token ON token.principal_id=hidden.grantee_principal_id
                            WHERE hidden.state=''D'' AND hidden.permission_name IN (''VIEW DEFINITION'',''VIEW SECURITY DEFINITION'',''CONTROL''))))
                        INSERT INTO #Gaps VALUES (DB_NAME(), NULL, N''Database definitions are only partially visible.'');
                    -- Database users inherit PUBLIC. A new-user baseline starts at PUBLIC itself.
                    INSERT INTO #RoleClosure
                    SELECT c.RequestId,c.UserId,DATABASE_PRINCIPAL_ID(''public'') FROM #RoleClosure c
                    WHERE c.MemberId<>DATABASE_PRINCIPAL_ID(''public'');
                    -- Iterative closure handles nested/diamond memberships without
                    -- duplicate grants, recursive depth limits or cycles.
                    WHILE 1 = 1
                    BEGIN
                        INSERT INTO #RoleClosure
                        SELECT DISTINCT c.RequestId, c.UserId, rm.role_principal_id
                        FROM #RoleClosure c
                        JOIN sys.database_role_members rm ON rm.member_principal_id = c.MemberId
                        WHERE NOT EXISTS (SELECT 1 FROM #RoleClosure seen
                            WHERE seen.RequestId = c.RequestId AND seen.MemberId = rm.role_principal_id);
                        IF @@ROWCOUNT = 0 BREAK;
                    END;

                    INSERT INTO #Gaps SELECT DISTINCT DB_NAME(), c.RequestId, N''Ownership can supply access beyond permission grants.''
                        FROM #RoleClosure c JOIN sys.database_principals owner ON owner.principal_id=c.MemberId
                        WHERE c.MemberId=1 OR EXISTS (SELECT 1 FROM sys.schemas s WHERE s.principal_id=c.MemberId
                            AND NOT (owner.is_fixed_role=1 AND s.name=owner.name))
                        OR EXISTS (SELECT 1 FROM sys.objects o WHERE o.principal_id=c.MemberId);
                    IF EXISTS (SELECT 1 FROM #RoleClosure)
                        INSERT INTO #ExecTargets
                        SELECT DB_NAME(),s.name,o.name,RTRIM(o.type)
                        FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id
                        WHERE o.is_ms_shipped=0 AND o.type IN (''P'',''PC'',''RF'',''X'',''FN'',''FS'',''AF'',''SN'');
                    INSERT INTO #PermissionRows
                    SELECT c.RequestId, DB_NAME(), s.name, o.name, col.name,
                           p.permission_name, p.state_desc,
                           CASE p.class WHEN 0 THEN ''Database'' WHEN 3 THEN ''Schema''
                                WHEN 1 THEN CASE WHEN p.minor_id=0 THEN ''Object'' ELSE ''Column'' END
                                ELSE p.class_desc END,
                           grantee.name, USER_NAME(p.grantor_principal_id), CASE WHEN c.MemberId=c.UserId AND @baseline=0 THEN 0 ELSE 1 END,
                           p.major_id, p.minor_id
                    FROM #RoleClosure c
                    JOIN sys.database_permissions p ON p.grantee_principal_id = c.MemberId
                    JOIN sys.database_principals grantee ON grantee.principal_id = c.MemberId
                    LEFT JOIN sys.all_objects o ON p.class = 1 AND p.major_id = o.object_id
                    LEFT JOIN sys.schemas s ON s.schema_id = CASE
                        WHEN p.class = 3 THEN p.major_id WHEN p.class = 1 THEN o.schema_id END
                    LEFT JOIN sys.all_columns col ON p.class = 1 AND p.major_id = col.object_id AND p.minor_id = col.column_id
                    WHERE p.state IN (''G'', ''W'', ''D'', ''R'')
                      -- Built-in public metadata visibility is not an application grant.
                      -- Negative object IDs identify SQL Server system objects, including
                      -- metadata objects hidden from sys.all_objects on some versions.
                      AND NOT (grantee.name = ''public'' AND p.class = 1 AND p.major_id < 0 AND p.state IN (''G'', ''W''))
                    UNION ALL
                    SELECT c.RequestId, DB_NAME(), NULL, NULL, NULL, perms.PermissionName,
                           CASE perms.PermissionState WHEN ''G'' THEN ''GRANT'' ELSE ''DENY'' END,
                           CASE WHEN r.name=''db_ddladmin'' THEN ''Fixed database role'' ELSE ''Database'' END,
                           r.name, NULL, 1, 0, 0
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
                    -- A table/column witness can distinguish a partial DML grant from
                    -- a broader grant whose every visible target is denied. Retrieve
                    -- this inventory only for databases with granular DML exceptions.
                    IF EXISTS (SELECT 1 FROM #RoleClosure c JOIN sys.database_permissions p
                        ON p.grantee_principal_id=c.MemberId WHERE p.class IN (1,3) AND p.state=''D''
                        AND p.permission_name IN (''SELECT'',''REFERENCES'',''UPDATE'',''DELETE'',''INSERT''))
                        INSERT INTO #DataColumns SELECT DB_NAME(),s.name,o.name,col.name
                        FROM sys.tables o JOIN sys.schemas s ON s.schema_id=o.schema_id
                        JOIN sys.columns col ON col.object_id=o.object_id WHERE o.is_ms_shipped=0;
                    DROP TABLE #RoleClosure;';
                EXEC sys.sp_executesql @Sql, N'@baseline bit', @baseline=@limited;
                FETCH NEXT FROM databases INTO @Database;
            END;
            CLOSE databases;
            DEALLOCATE databases;
            SELECT pr.Name,r.DatabaseName,r.SchemaName,r.ObjectName,r.ColumnName,r.PermissionName,r.PermissionState,
                   r.ScopeName,r.Grantee,r.Grantor,r.Inherited,r.MajorId,r.MinorId
                FROM #PermissionRows r JOIN #Principals pr ON pr.RequestId=r.RequestId;
            SELECT Name, LoginName FROM #Principals WHERE LoginSid IS NOT NULL;
            SELECT DatabaseName,SchemaName,ObjectName,ObjectType FROM #ExecTargets;
            SELECT DatabaseName,SchemaName,ObjectName,ColumnName FROM #DataColumns;
            SELECT g.DatabaseName,pr.Name,g.Reason FROM #Gaps g LEFT JOIN #Principals pr ON g.RequestId=pr.RequestId;
            SELECT name FROM sys.databases WHERE @limited=0 OR database_id IN (SELECT DB_ID(Name) FROM #RequestedDatabases);
            """;

        var rows = principalNames.ToDictionary(n => n, _ => new List<(string Database, SqlPermissionEvidence Evidence)>(), StringComparer.Ordinal);
        var logins = new Dictionary<string, string>(StringComparer.Ordinal);
        var targets = new Dictionary<string, List<SqlCallableObject>>(StringComparer.Ordinal);
        var columns = new Dictionary<string, List<SqlTableColumn>>(StringComparer.Ordinal);
        var gaps = new List<(string? Database, string? Principal, string Reason)>();
        await using var inspectionCommand = new SqlCommand(query, connection);
        inspectionCommand.Parameters.Add("@limited", System.Data.SqlDbType.Bit).Value = databases is not null;
        await using var reader = await inspectionCommand.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var scope = reader.GetString(7);
            var name = reader.IsDBNull(3) ? null : reader.GetString(3);
            var column = reader.IsDBNull(4) ? null : reader.GetString(4);
            if (scope is not ("Database" or "Schema" or "Fixed database role")) name ??= $"ID {reader.GetInt32(11)}";
            if (scope == "Column") column ??= $"ID {reader.GetInt32(12)}";
            var evidence = new SqlPermissionEvidence(scope, reader.IsDBNull(2) ? null : reader.GetString(2),
                name, column, reader.GetString(6), reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetBoolean(10), reader.GetString(5));
            rows[reader.GetString(0)].Add((reader.GetString(1), evidence));
        }
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct)) logins.Add(reader.GetString(0), reader.GetString(1));
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var database = reader.GetString(0);
            if (!targets.TryGetValue(database, out var items)) targets[database] = items = [];
            items.Add(new(reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (!columns.TryGetValue(reader.GetString(0), out var items)) columns[reader.GetString(0)] = items = [];
            items.Add(new(reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct))
            gaps.Add((reader.IsDBNull(0) ? null : reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2)));

        await reader.NextResultAsync(ct);
        var catalogDatabases = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(ct)) catalogDatabases.Add(reader.GetString(0));

        return principalNames.ToDictionary(n => n, n => DescribePermissions(logins.GetValueOrDefault(n) ?? n, logins.ContainsKey(n), rows[n], targets, columns,
            gaps.Where(g => g.Principal == null || g.Principal == n).Select(g => (g.Database, g.Reason)).ToList()) with { CatalogDatabases = catalogDatabases }, StringComparer.Ordinal);
    }
}
