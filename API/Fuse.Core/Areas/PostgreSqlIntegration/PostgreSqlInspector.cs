using Fuse.Core.Models;
using Fuse.Core.Responses;
using Npgsql;
namespace Fuse.Core.Areas.PostgreSqlIntegration;

/// <summary>Fixed, bounded catalog queries only. Never reads application rows, password hashes or pg_authid.</summary>
public sealed class PostgreSqlInspector : IPostgreSqlInspector
{
    private const int RoleLimit = 500, RowLimit = 5000;
    public async Task<PostgreSqlConnectionTestResult> ValidateAsync(Uri endpoint, string username, string? password, CancellationToken ct = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(PostgreSqlConnectionSettings.Build(endpoint, username, password));
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(ct);
            return new(true, true, "connected", null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (Expected(e)) { return new(false, false, Status(e), Message(e)); }
    }

    internal static bool Expected(Exception e) => e is NpgsqlException or TimeoutException or OperationCanceledException;
    internal static string Status(Exception e) => e is PostgresException p ? p.SqlState switch
    {
        "28P01" or "28000" => "authentication-failed", "42501" => "denied", "3D000" => "database-not-found",
        "57014" => "timeout", _ => "unavailable"
    } : e is TimeoutException or OperationCanceledException ? "timeout" : "unavailable";
    private static string Message(Exception e) => Status(e) switch
    {
        "authentication-failed" => "PostgreSQL rejected authentication.",
        "denied" => "PostgreSQL denied the connection or catalog inspection.",
        "database-not-found" => "The selected database does not exist.",
        "timeout" => "PostgreSQL inspection timed out.",
        _ => "Cannot inspect PostgreSQL. Check the endpoint, TLS configuration and server availability."
    };

    public static IReadOnlyList<PostgreSqlAccountMatchResponse> Match(IReadOnlyList<Models.Account> accounts,
        IReadOnlyList<PostgreSqlRoleResponse> roles, string status) => accounts.Select(a =>
    {
        var role = roles.FirstOrDefault(r => string.Equals(r.Name, a.UserName, StringComparison.Ordinal));
        return new PostgreSqlAccountMatchResponse(a.Id, a.UserName,
            string.IsNullOrEmpty(a.UserName) ? "no-username" : role is null ? (status == "complete" ? "missing" : "unknown")
            : !role.CanLogin ? "no-login" : !role.CanConnect ? "connect-denied" : "matched");
    }).ToArray();

    public async Task<PostgreSqlDiscoveryResponse> DiscoverAsync(Models.PostgreSqlIntegration integration,
        IReadOnlyList<Models.Account> accounts, CancellationToken ct = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        var roles = new List<PostgreSqlRoleResponse>();
        var memberships = new List<PostgreSqlMembershipResponse>();
        var permissions = new List<PostgreSqlPrivilegeResponse>();
        var warnings = new List<string> {
            "Roles and memberships belong to the cluster. Database, schema and relation privileges are scoped to the selected database.",
            "Privileges include inherited and PUBLIC grants and ownership. They do not prove login through pg_hba.conf or application access, column permissions, sequence permissions or row security. SET ROLE is reported but never executed." };
        string connectionStatus = "connected", roleStatus = "unknown", membershipStatus = "unknown", permissionStatus = "unknown";
        var database = PostgreSqlConnectionSettings.Database(integration.Endpoint);
        await using var connection = new NpgsqlConnection(PostgreSqlConnectionSettings.Build(integration.Endpoint, integration.Username, integration.Password));
        try
        {
            await connection.OpenAsync(token);
            if (connection.PostgreSqlVersion.Major < 16)
            {
                warnings.Add("PostgreSQL 16 or newer is required for per-membership inheritance and SET ROLE inspection.");
                roleStatus = membershipStatus = permissionStatus = "unsupported-version";
            }
            else
            {
                roleStatus = await Read("""
                    SELECT rolname, rolcanlogin, rolinherit, rolsuper, rolcreaterole, rolcreatedb, rolbypassrls,
                           has_database_privilege(oid, current_database(), 'CONNECT')
                    FROM pg_catalog.pg_roles ORDER BY rolname COLLATE "C" LIMIT 501
                    """, RoleLimit, r => roles.Add(new(r.GetString(0), r.GetBoolean(1), r.GetBoolean(2), r.GetBoolean(3),
                        r.GetBoolean(4), r.GetBoolean(5), r.GetBoolean(6), r.GetBoolean(7),
                        accounts.Where(a => a.UserName == r.GetString(0)).Select(a => a.Id).ToArray())));
                if (roleStatus == "complete")
                {
                    membershipStatus = await Read("""
                        WITH RECURSIVE memberships(member,roleid) AS (
                          SELECT member,roleid FROM pg_catalog.pg_auth_members
                          UNION
                          SELECT m.member,a.roleid FROM memberships m JOIN pg_catalog.pg_auth_members a ON a.member=m.roleid
                        )
                        SELECT u.rolname,r.rolname,
                          EXISTS(SELECT 1 FROM pg_catalog.pg_auth_members a WHERE a.member=m.member AND a.roleid=m.roleid),
                          pg_has_role(m.member,m.roleid,'USAGE'), pg_has_role(m.member,m.roleid,'SET')
                        FROM memberships m JOIN pg_catalog.pg_roles u ON u.oid=m.member JOIN pg_catalog.pg_roles r ON r.oid=m.roleid
                        ORDER BY u.rolname COLLATE "C",r.rolname COLLATE "C" LIMIT 5001
                        """, RowLimit, r => memberships.Add(new(r.GetString(0), r.GetString(1), r.GetBoolean(2), r.GetBoolean(3), r.GetBoolean(4))));
                    permissionStatus = await Read("""
                        WITH objects AS (
                          SELECT 'database' AS kind,current_database()::text AS name,d.oid,ARRAY['CONNECT','CREATE','TEMP'] AS privs FROM pg_catalog.pg_database d WHERE datname=current_database()
                          UNION ALL
                          SELECT 'schema',quote_ident(nspname),oid,ARRAY['USAGE','CREATE'] FROM pg_catalog.pg_namespace WHERE nspname !~ '^pg_' AND nspname <> 'information_schema'
                          UNION ALL
                          SELECT 'relation',quote_ident(n.nspname)||'.'||quote_ident(c.relname),c.oid,ARRAY['SELECT','INSERT','UPDATE','DELETE','TRUNCATE','REFERENCES','TRIGGER']
                          FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
                          WHERE n.nspname !~ '^pg_' AND n.nspname <> 'information_schema' AND c.relkind IN ('r','p','v','m','f')
                        )
                        SELECT r.rolname,o.kind,o.name,ARRAY(SELECT p FROM unnest(o.privs) p WHERE CASE o.kind
                          WHEN 'database' THEN has_database_privilege(r.oid,o.oid,p)
                          WHEN 'schema' THEN has_schema_privilege(r.oid,o.oid,p)
                          ELSE has_table_privilege(r.oid,o.oid,p) END)
                        FROM pg_catalog.pg_roles r CROSS JOIN objects o
                        ORDER BY r.rolname COLLATE "C",o.kind,o.name COLLATE "C" LIMIT 5001
                        """, RowLimit, r => permissions.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetFieldValue<string[]>(3))));
                }
                else warnings.Add("Membership and permission inspection requires a complete role list (limit 500).");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (Expected(e)) { connectionStatus = Status(e); warnings.Add(Message(e)); }
        foreach (var (section, status) in new[] { ("Roles",roleStatus),("Memberships",membershipStatus),("Permissions",permissionStatus) })
            if (status != "complete") warnings.Add($"{section}: {status}. Unobserved entries are unknown, not absent.");
        return new(integration.Id, DateTime.UtcNow, database, connectionStatus, roleStatus, membershipStatus, permissionStatus,
            roles, memberships, permissions, Match(accounts, roles, roleStatus), warnings);

        async Task<string> Read(string sql, int limit, Action<NpgsqlDataReader> add)
        {
            try
            {
                await using var command = new NpgsqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync(token);
                var count = 0;
                while (await reader.ReadAsync(token))
                {
                    if (++count > limit) return "truncated";
                    add(reader);
                }
                return "complete";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception e) when (Expected(e)) { return Status(e); }
        }
    }
}
