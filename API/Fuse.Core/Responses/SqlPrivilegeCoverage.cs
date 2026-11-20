using System.Text.Json.Serialization;

namespace Fuse.Core.Responses;

public enum SqlCoverageLevel { None, Some, All, Unknown }

public record SqlProcedureAccess(int Executable, int Total);

public record SqlPermissionEvidence(
    string Scope,
    string? Schema,
    string? ObjectName,
    string? ColumnName,
    string State,
    string Grantee,
    string? Grantor,
    bool Inherited,
    string Permission
);

public record SqlPrivilegeCoverage(
    string Database,
    string Permission,
    SqlCoverageLevel Level,
    IReadOnlyList<SqlPermissionEvidence> Evidence,
    string Basis
)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SqlProcedureAccess? ProcedureAccess { get; init; }
}
