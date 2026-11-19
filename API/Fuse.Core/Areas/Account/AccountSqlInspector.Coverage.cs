using Microsoft.Data.SqlClient;

namespace Fuse.Core.Areas.Account;

public partial class AccountSqlInspector
{
    public async Task<SqlPrincipalPermissions?> GetNewUserBaselineAsync(Models.SqlIntegration integration, IReadOnlyList<string> databases, CancellationToken ct = default)
    {
        try
        {
            await using var connection = new SqlConnection(integration.ConnectionString);
            await connection.OpenAsync(ct);
            var rows = await ReadPrincipalPermissionsAsync(connection, ["public"], ct, databases);
            var publicGrants = rows["public"];
            if (publicGrants.InspectionError is not null) return null;
            return new(null, true, [.. publicGrants.Grants, .. databases.Select(d => new SqlActualGrant(d, null, [Models.Privilege.Connect]))]);
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is SqlException or InvalidOperationException or ArgumentException or TimeoutException)
        { return null; }
    }

    public async Task<string?> InspectGrantCoverageAsync(Models.SqlIntegration integration, CancellationToken ct = default)
    {
        try
        {
            await using var connection = new SqlConnection(integration.ConnectionString);
            await connection.OpenAsync(ct);
            // An accessible sys view can silently omit other principals, databases and grants.
            // Check visibility in every online user database this server-wide review covers.
            const string query = """
                IF ISNULL(HAS_PERMS_BY_NAME(NULL,NULL,'VIEW ANY DATABASE'),0) <> 1
                    OR ISNULL(HAS_PERMS_BY_NAME(NULL,NULL,'VIEW ANY DEFINITION'),0) <> 1
                    OR EXISTS(SELECT 1 FROM sys.databases WHERE database_id > 4 AND (state <> 0 OR ISNULL(HAS_DBACCESS(name),0) <> 1))
                BEGIN SELECT 0; RETURN; END;
                DECLARE @db sysname, @sql nvarchar(max), @visible int;
                DECLARE databases CURSOR LOCAL FAST_FORWARD FOR
                    SELECT name FROM sys.databases WHERE database_id > 4;
                OPEN databases;
                FETCH NEXT FROM databases INTO @db;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @sql = N'USE ' + QUOTENAME(@db) + N'; SELECT @v=ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),''DATABASE'',''VIEW DEFINITION''),0);';
                    EXEC sys.sp_executesql @sql, N'@v int OUTPUT', @visible OUTPUT;
                    IF @visible <> 1 BEGIN SELECT 0; RETURN; END;
                    FETCH NEXT FROM databases INTO @db;
                END;
                SELECT 1;
                """;
            await using var command = new SqlCommand(query, connection) { CommandTimeout = 10 };
            return await command.ExecuteScalarAsync(ct) is 1 ? null :
                "SQL Server metadata visibility is incomplete. Verify server definition visibility and access to each user database.";
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is SqlException or InvalidOperationException or ArgumentException or TimeoutException)
        { return "SQL Server catalog visibility could not be verified."; }
    }
}
