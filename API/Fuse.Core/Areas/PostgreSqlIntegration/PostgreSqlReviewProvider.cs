using System.Text;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Models;
using Npgsql;
using static Fuse.Core.Areas.IntegrationReviews.DataStoreReview;

namespace Fuse.Core.Areas.PostgreSqlIntegration;

public sealed class PostgreSqlReviewProvider : IIntegrationReviewProvider
{
    public string Provider => "postgresql";
    internal static readonly string[] TablePrivileges = ["SELECT", "INSERT", "UPDATE", "DELETE"];
    private static readonly ReviewField Schema = new("schema", "Schema");
    private static readonly ReviewField Table = new("table", "Table");
    private static readonly ReviewField Expected = new("expected", "Expected access", Choices: ["allow", "deny"]);
    public IReadOnlyList<ReviewCheck> Checks { get; } =
    [
        new("table-access", "PostgreSQL table access", [Schema, Table, new("privilege", "Privilege", Choices: TablePrivileges), Expected], true),
        new("column-access", "PostgreSQL column access", [Schema, Table, new("column", "Column"),
            new("privilege", "Privilege", Choices: ["SELECT", "INSERT", "UPDATE", "REFERENCES"]), Expected], true),
        new("sequence-access", "PostgreSQL sequence access", [Schema, new("sequence", "Sequence"),
            new("privilege", "Privilege", Choices: ["USAGE", "SELECT", "UPDATE"]), Expected], true),
        new("routine-access", "PostgreSQL routine execution", [Schema, new("routine", "Routine"),
            new("arguments", "Input argument types", Required: false, Hint: "Comma-separated PostgreSQL types as returned by oidvectortypes. Leave empty for zero arguments."), Expected], true),
        new("row-security", "PostgreSQL row security configuration", [Schema, Table,
            new("mode", "Expected row security", Choices: ["disabled", "enabled", "forced"])])
    ];

    public string? Validate(Snapshot snapshot, ReviewInput input)
    {
        var item = snapshot.PostgreSqlIntegrations.FirstOrDefault(i => i.Id == input.IntegrationId);
        if (ValidateScope(snapshot, item?.DataStoreId, "PostgreSQL", input, Checks) is { } error) return error;
        if (input.Fields.Where(p => p.Key is "schema" or "table" or "column" or "sequence" or "routine")
            .Any(p => Encoding.UTF8.GetByteCount(p.Value) > 63)) return "PostgreSQL identifiers must fit in 63 UTF-8 bytes.";
        var schema = input.Fields["schema"];
        return schema == "information_schema" || schema.StartsWith("pg_", StringComparison.Ordinal)
            ? "Select an application schema." : null;
    }

    public async Task<ReviewEvidence> InspectAsync(Snapshot snapshot, ReviewInput input, CancellationToken ct)
    {
        if (Validate(snapshot, input) is { } error) return Unknown(error);
        var item = snapshot.PostgreSqlIntegrations.Single(i => i.Id == input.IntegrationId);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            await using var connection = new NpgsqlConnection(PostgreSqlConnectionSettings.Build(item.Endpoint, item.Username, item.Password));
            await connection.OpenAsync(deadline.Token);
            if (connection.PostgreSqlVersion.Major < 16) return Unknown("Membership review requires PostgreSQL 16 or later.");
            if (input.Kind == "row-security") return await CheckRowSecurity(connection, input.Fields, deadline.Token);
            var account = ScopedAccount(snapshot, input.AccountId, item.DataStoreId)!;
            var facts = await Read(connection, input, account.UserName!, deadline.Token);
            return facts is null ? Compare(false, "The documented role or database object was not found.") :
                Evidence(input, account.UserName!, facts);
        }
        catch (Exception e) when (!ct.IsCancellationRequested && PostgreSqlInspector.Expected(e))
        { return Unknown("PostgreSQL access inspection: " + PostgreSqlInspector.Status(e) + "."); }
    }

    internal sealed record Access(string Database, uint RoleId, uint ObjectId, string ChangeStamp, bool Login,
        bool Connect, bool SchemaUsage, bool Granted, bool InspectorOwns, bool IsTable, bool RowSecurity, string[] Sources, string[] Assumable)
    {
        public bool Usable => Login && Connect && SchemaUsage && Granted;
    }

    internal static ReviewEvidence Evidence(ReviewInput input, string role, Access fact)
    {
        var privilege = input.Fields.GetValueOrDefault("privilege", "EXECUTE");
        var details = new List<string> { $"Database {fact.Database}, role {role}.", $"Login: {fact.Login}. CONNECT: {fact.Connect}. Schema USAGE: {fact.SchemaUsage}. Object privilege: {fact.Granted}." };
        details.AddRange(fact.Sources.Select(s => "Privilege source: " + s + "."));
        if (fact.Assumable.Length > 0) details.Add("Available after SET ROLE: " + string.Join(", ", fact.Assumable) + ".");
        if (fact.RowSecurity) details.Add("Row security is enabled. Visible rows depend on policies and session context.");
        return Compare(fact.Usable == (input.Fields["expected"] == "allow"),
            $"{privilege} {(fact.Usable ? "available" : "unavailable")} at login.", details.ToArray());
    }

    private static async Task<ReviewEvidence> CheckRowSecurity(NpgsqlConnection connection, Dictionary<string,string> fields, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT c.relrowsecurity, c.relforcerowsecurity,
              ARRAY(SELECT polname::text FROM pg_policy WHERE polrelid=c.oid ORDER BY polname LIMIT 50)
            FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname=@schema AND c.relname=@name AND c.relkind IN ('r','p')
            """, connection);
        command.Parameters.AddWithValue("schema", fields["schema"]);
        command.Parameters.AddWithValue("name", fields["table"]);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return Compare(false, "The documented table was not found.");
        var mode = !reader.GetBoolean(0) ? "disabled" : reader.GetBoolean(1) ? "forced" : "enabled";
        return Compare(mode == fields["mode"], "Row security is " + mode + ".",
            "Configured policies (up to 50): " + string.Join(", ", reader.GetFieldValue<string[]>(2)));
    }

    internal static async Task<Access?> Read(NpgsqlConnection connection, ReviewInput input, string username, CancellationToken ct,
        NpgsqlTransaction? transaction = null)
    {
        // Select an object only in the connected database. Every identifier and the privilege come in as parameter values.
        var kind = input.Kind;
        var privilege = input.Fields.GetValueOrDefault("privilege", "EXECUTE");
        var expressions = kind switch
        {
            "table-access" => ("table", "has_table_privilege(R,c.oid,@privilege)"),
            "column-access" => ("table", "has_column_privilege(R,c.oid,a.attnum,@privilege)"),
            "sequence-access" => ("sequence", "has_sequence_privilege(R,c.oid,@privilege)"),
            "routine-access" => ("routine", "has_function_privilege(R,c.oid,@privilege)"),
            _ => throw new ArgumentException("Unsupported review kind.")
        };
        string sqlObject = kind switch
        {
            "routine-access" => """
                SELECT p.oid, p.proowner AS owner, COALESCE(p.proacl,acldefault('f',p.proowner)) AS acl, p.xmin::text AS stamp,
                       p.pronamespace AS namespace,false AS is_table,false AS rls
                FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
                WHERE n.nspname=@schema AND p.proname=@name AND oidvectortypes(p.proargtypes)=@args
                """,
            _ => """
                SELECT t.oid, t.relowner AS owner, COALESCE(t.relacl,acldefault(CASE WHEN t.relkind='S' THEN 's'::"char" ELSE 'r'::"char" END,t.relowner)) AS acl,
                       t.xmin::text AS stamp, t.relnamespace AS namespace, t.relkind IN ('r','p') AS is_table, t.relrowsecurity AS rls
                FROM pg_class t JOIN pg_namespace n ON n.oid=t.relnamespace
                WHERE n.nspname=@schema AND t.relname=@name
                """ + (kind == "sequence-access" ? " AND t.relkind='S'" : " AND t.relkind IN ('r','p','v','m','f')")
        };
        var access = expressions.Item2;
        var column = kind == "column-access";
        var objectAcls = column ? "c.acl || COALESCE(a.attacl,'{}'::aclitem[])" : "c.acl";
        // Screen source roles through PostgreSQL's effective privilege functions. Ownership alone is not proof of a retained privilege.
        var sql = $$"""
            WITH obj AS ({{sqlObject}})
            SELECT current_database()::text,u.oid,c.oid,c.stamp||':'||u.oid::text||':'||c.owner::text,
                   u.rolcanlogin,has_database_privilege(u.oid,current_database(),'CONNECT'),
                   has_schema_privilege(u.oid,c.namespace,'USAGE'),{{access.Replace("R,", "u.oid,")}},
                   pg_get_userbyid(c.owner)=current_user,c.is_table,c.rls,
                   ARRAY(SELECT DISTINCT reason FROM (
                        SELECT CASE WHEN x.grantee=0 THEN 'PUBLIC'
                                 WHEN x.grantee=c.owner AND x.grantee=u.oid THEN 'owner'
                                 WHEN x.grantee=u.oid THEN 'direct grant'
                                 ELSE 'inherited role '||pg_get_userbyid(x.grantee) END AS reason
                        FROM aclexplode({{objectAcls}}) x WHERE x.privilege_type=@privilege AND
                             CASE WHEN x.grantee=0 THEN true ELSE pg_has_role(u.oid,x.grantee,'USAGE') END
                        UNION ALL SELECT 'superuser' WHERE u.rolsuper
                   ) s ORDER BY reason LIMIT 32),
                   ARRAY(SELECT r.rolname::text FROM pg_roles r WHERE NOT {{access.Replace("R,", "u.oid,")}}
                         AND r.oid<>u.oid AND pg_has_role(u.oid,r.oid,'SET') AND has_schema_privilege(r.oid,c.namespace,'USAGE')
                         AND {{access.Replace("R,", "r.oid,")}} ORDER BY r.rolname LIMIT 32)
            FROM obj c CROSS JOIN pg_roles u
            {{(column ? "JOIN pg_attribute a ON a.attrelid=c.oid AND a.attname=@column AND a.attnum>0 AND NOT a.attisdropped" : "")}}
            WHERE u.rolname=@username
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("schema", input.Fields["schema"]);
        command.Parameters.AddWithValue("name", input.Fields[expressions.Item1]);
        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue("privilege", privilege);
        if (column) command.Parameters.AddWithValue("column", input.Fields["column"]);
        if (kind == "routine-access") command.Parameters.AddWithValue("args", input.Fields.GetValueOrDefault("arguments", ""));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(reader.GetString(0), reader.GetFieldValue<uint>(1), reader.GetFieldValue<uint>(2), reader.GetString(3),
            reader.GetBoolean(4), reader.GetBoolean(5), reader.GetBoolean(6), reader.GetBoolean(7), reader.GetBoolean(8),
            reader.GetBoolean(9), reader.GetBoolean(10), reader.GetFieldValue<string[]>(11), reader.GetFieldValue<string[]>(12));
    }
}
