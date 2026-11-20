using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.Account;

public partial class AccountSqlInspector
{
    private sealed record SqlCallableObject(string Schema, string Name, string Type)
    {
        public bool IsProcedure => Type is "P" or "PC" or "RF" or "X";
    }

    private readonly record struct CoverageAssessment(SqlCoverageLevel Level, string Basis, SqlProcedureAccess? Procedures = null);

    private sealed record SqlTableColumn(string Schema, string Object, string Column);

    private static SqlPrincipalPermissions DescribePermissions(string principal, bool exists,
        List<(string Database, SqlPermissionEvidence Evidence)> rows, Dictionary<string, List<SqlCallableObject>> targets,
        Dictionary<string, List<SqlTableColumn>> columns, List<(string? Database, string Reason)> gaps)
    {
        var grants = new List<SqlActualGrant>();
        var coverage = new List<SqlPrivilegeCoverage>();
        foreach (var database in rows.GroupBy(r => r.Database).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var evidence = database.Select(r => r.Evidence).OrderBy(e => e.Schema, StringComparer.Ordinal)
                .ThenBy(e => e.ObjectName, StringComparer.Ordinal).ThenBy(e => e.ColumnName, StringComparer.Ordinal)
                .ThenBy(e => e.Grantee, StringComparer.Ordinal).ThenBy(e => e.State, StringComparer.Ordinal)
                .ThenBy(e => e.Grantor, StringComparer.Ordinal).ToArray();

            // Desired-state comparisons use named, database-level rights only. A
            // column GRANT (or an object DENY) is never a database-level operation.
            var grant = new SqlActualGrant(database.Key, null, []);
            foreach (var row in evidence.Where(e => e.Scope == "Database"))
            {
                if (MapSqlPermissionToPrivilege(row.Permission) is not Privilege privilege) continue;
                if (IsGrant(row))
                {
                    grant.Privileges.Add(privilege);
                    if (row.Inherited) grant.Inherited.Add(privilege);
                }
                if (row.State != "GRANT") grant.Protected.Add(privilege);
            }
            if (grant.Privileges.Count != 0 || grant.Protected.Count != 0) grants.Add(grant);

            var complete = gaps.All(g => g.Database != null && g.Database != database.Key);
            var permissions = evidence.Select(e => e.Permission).ToHashSet(StringComparer.Ordinal);
            // CONTROL covers subordinate rights on its securable. Retain the
            // original CONTROL row as provenance, never as an assignable subgrant.
            var controls = evidence.Where(e => e.Permission == "CONTROL").ToArray();
            if (controls.Length != 0)
                permissions.UnionWith(["SELECT", "INSERT", "UPDATE", "DELETE", "EXECUTE", "ALTER", "REFERENCES"]);
            foreach (var permission in permissions.Order(StringComparer.Ordinal))
            {
                var observations = evidence.Where(e => e.Permission == permission ||
                    (e.Permission == "CONTROL" && permission is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "EXECUTE" or "ALTER" or "REFERENCES")).ToArray();
                var assessment = complete
                    ? EvaluateCoverage(permission, observations, targets.GetValueOrDefault(database.Key) ?? [], columns.GetValueOrDefault(database.Key) ?? [])
                    : new CoverageAssessment(SqlCoverageLevel.Unknown, "Incomplete");
                coverage.Add(new(database.Key, permission, assessment.Level, observations, assessment.Basis) { ProcedureAccess = assessment.Procedures });
            }
        }
        var warnings = gaps.Select(g => g.Database == null ? g.Reason : $"{g.Database}: {g.Reason}").Distinct().Order(StringComparer.Ordinal).ToArray();
        return new(principal, exists, grants) { Coverage = coverage, CoverageWarning = warnings.Length == 0 ? null : string.Join(" ", warnings) };
    }

    private static bool IsGrant(SqlPermissionEvidence e) => e.State is "GRANT" or "GRANT_WITH_GRANT_OPTION";

    private static CoverageAssessment EvaluateCoverage(string permission, SqlPermissionEvidence[] rows,
        List<SqlCallableObject> targets, List<SqlTableColumn> columns)
    {
        // Resolve catalog scopes and permission states only where their meaning
        // is known. In particular a column REVOKE is a stored exception.
        if (rows.Any(e => e.Scope is not ("Database" or "Schema" or "Object" or "Column" or "Fixed database role")
            || e.State == "REVOKE" || (e.Scope is "Schema" or "Object" or "Column" && e.Schema == null)
            || (e.Permission == "CONTROL" && e.Permission != permission && (e.State == "DENY" || (e.Scope == "Object" && permission != "EXECUTE")))))
            return new(SqlCoverageLevel.Unknown, "Incomplete");

        var grants = rows.Where(IsGrant).ToArray();
        var denies = rows.Where(e => e.State == "DENY").ToArray();
        // db_ddladmin can alter objects, but membership does not grant ALTER on
        // DATABASE. Its granular role rights cannot settle a conflicting DENY.
        if (grants.Any(e => e.Scope == "Fixed database role") && denies.Length != 0)
            return new(SqlCoverageLevel.Unknown, "Incomplete");
        if (permission == "EXECUTE" && rows.All(e => e.Scope != "Column"))
            return ExecutableCoverage(grants, denies, targets);
        if (grants.Length == 0 || denies.Any(e => e.Scope == "Database"))
            return new(SqlCoverageLevel.None, "Restrictions");
        var databaseGrant = grants.Any(e => e.Scope == "Database");
        if (databaseGrant && denies.Length == 0) return new(SqlCoverageLevel.All, "DatabaseGrant");

        // SQL Server preserves the historic column GRANT exception to an object
        // DENY. Matching a column requires the object as well as the schema.
        var surviving = grants.Where(g => !denies.Any(d => Covers(d, g) &&
            !(d.Scope == "Object" && g.Scope == "Column"))).ToArray();
        if (surviving.Length == 0) return new(SqlCoverageLevel.None, "Restrictions");
        // A database or schema grant can have narrower exceptions. Without an
        // inventory of applicable targets we cannot prove any surviving access.
        if (denies.Any(d => surviving.Any(g => Covers(g, d))))
        {
            if (permission is "SELECT" or "REFERENCES" or "UPDATE" or "DELETE" or "INSERT" &&
                HasMixedTableAccess(grants, denies, columns))
                return new(SqlCoverageLevel.Some, "ScopedGrants");
            return new(SqlCoverageLevel.Unknown, "Incomplete");
        }
        return new(SqlCoverageLevel.Some, "ScopedGrants");
    }

    private static bool Covers(SqlPermissionEvidence parent, SqlPermissionEvidence child)
    {
        if (parent.Scope == "Database") return true;
        if (parent.Schema != child.Schema) return false;
        return parent.Scope switch
        {
            "Schema" => true,
            "Object" => parent.ObjectName == child.ObjectName,
            "Column" => parent.ObjectName == child.ObjectName && parent.ColumnName == child.ColumnName,
            _ => false
        };
    }

    private sealed class PermissionScopes(SqlPermissionEvidence[] evidence)
    {
        private readonly bool _database = evidence.Any(e => e.Scope == "Database");
        private readonly HashSet<string?> _schemas = evidence.Where(e => e.Scope == "Schema").Select(e => e.Schema).ToHashSet();
        private readonly HashSet<(string?, string?)> _objects = evidence.Where(e => e.Scope == "Object").Select(e => (e.Schema, e.ObjectName)).ToHashSet();
        private readonly HashSet<(string?, string?, string?)> _columns = evidence.Where(e => e.Scope == "Column").Select(e => (e.Schema, e.ObjectName, e.ColumnName)).ToHashSet();
        public bool Covers(string schema, string name) => _database || _schemas.Contains(schema) || _objects.Contains((schema, name));
        public bool CoversColumn(SqlTableColumn c) => _columns.Contains((c.Schema, c.Object, c.Column));
        public bool CoversSchema(string schema) => _database || _schemas.Contains(schema);
    }

    private static bool HasMixedTableAccess(SqlPermissionEvidence[] grants, SqlPermissionEvidence[] denies, List<SqlTableColumn> columns)
    {
        var allowed = new PermissionScopes(grants);
        var denied = new PermissionScopes(denies);
        var foundAllowed = false;
        var foundDenied = false;
        foreach (var column in columns)
        {
            // At a table, a column-level GRANT can override an object-level DENY.
            var accessible = !denied.CoversColumn(column) && !denied.CoversSchema(column.Schema) &&
                (allowed.CoversColumn(column) || (allowed.Covers(column.Schema, column.Object) && !denied.Covers(column.Schema, column.Object)));
            if (accessible) foundAllowed = true;
            else foundDenied = true;
            if (foundAllowed && foundDenied) return true;
        }
        return false;
    }

    private static CoverageAssessment ExecutableCoverage(SqlPermissionEvidence[] grants,
        SqlPermissionEvidence[] denies, List<SqlCallableObject> targets)
    {
        // A grant can cover many procedures, and the same procedure can be granted
        // through multiple roles. Count catalog objects, independently of evidence rows.
        if (grants.Length == 0 || denies.Any(e => e.Scope == "Database"))
            return new(SqlCoverageLevel.None, "Restrictions", new(0, targets.Count(t => t.IsProcedure)));
        if (denies.Length == 0 && grants.Any(e => e.Scope == "Database"))
        {
            var total = targets.Count(t => t.IsProcedure);
            return new(SqlCoverageLevel.All, "DatabaseGrant", new(total, total));
        }

        var allowed = new PermissionScopes(grants);
        var denied = new PermissionScopes(denies);
        var reachable = 0;
        var uncovered = 0;
        var totalProcedures = 0;
        var executableProcedures = 0;
        var ambiguous = false;
        foreach (var target in targets)
        {
            if (target.Type == "SN") { ambiguous = true; continue; }
            var accessible = allowed.Covers(target.Schema, target.Name) && !denied.Covers(target.Schema, target.Name);
            if (accessible) reachable++;
            else uncovered++;
            if (target.IsProcedure)
            {
                totalProcedures++;
                if (accessible) executableProcedures++;
            }
        }
        // A synonym's base object may be another database's procedure. We can
        // still establish Some from known objects without claiming an exact count.
        var procedures = ambiguous ? null : new SqlProcedureAccess(executableProcedures, totalProcedures);
        if (reachable > 0 && uncovered > 0) return new(SqlCoverageLevel.Some, "CurrentObjects", procedures);
        if (ambiguous) return new(SqlCoverageLevel.Unknown, "Incomplete");
        return new(reachable > 0 ? SqlCoverageLevel.All : SqlCoverageLevel.None, "CurrentObjects", procedures);
    }
}
