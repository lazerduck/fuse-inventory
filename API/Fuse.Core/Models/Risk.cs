namespace Fuse.Core.Models;

public enum RiskImpact { Low, Medium, High, Critical }

public enum RiskLikelihood { Low, Medium, High }

public enum RiskStatus { Identified, Mitigated, Accepted, Closed }

public record Risk
(
    Guid Id,
    string Title,
    string? Description,
    RiskImpact Impact,
    RiskLikelihood Likelihood,
    RiskStatus Status,
    Guid OwnerPositionId,
    Guid? ApproverPositionId,
    string TargetType,
    Guid TargetId,
    string? Mitigation,
    DateTime? ReviewDate,
    DateTime? ApprovalDate,
    HashSet<Guid> TagIds,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
