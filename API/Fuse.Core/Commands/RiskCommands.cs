using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateRisk(
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
    HashSet<Guid>? TagIds = null,
    string? Notes = null
);

public record UpdateRisk(
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
    HashSet<Guid>? TagIds = null,
    string? Notes = null
);

public record DeleteRisk(
    Guid Id
);
