namespace Fuse.Core.Responses;

public enum SqlCoverageLevel { None, Some, All, Unknown }

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
);
