using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class RiskService : IRiskService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;
    private readonly IPositionService _positionService;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public RiskService(IFuseStore fuseStore, ITagService tagService, IPositionService positionService, IAuditService auditService, ICurrentUser currentUser)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
        _positionService = positionService;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<Risk>> GetRisksAsync()
        => (await _fuseStore.GetAsync()).Risks;

    public async Task<Risk?> GetRiskByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).Risks.FirstOrDefault(r => r.Id == id);

    public async Task<IReadOnlyList<Risk>> GetRisksByTargetAsync(string targetType, Guid targetId)
        => (await _fuseStore.GetAsync()).Risks
            .Where(r => r.TargetType == targetType && r.TargetId == targetId)
            .ToList();

    public async Task<Result<Risk>> CreateRiskAsync(CreateRisk command)
    {
        var tagIds = command.TagIds ?? new HashSet<Guid>();

        var validation = await ValidateRiskCommand(command.OwnerPositionId, command.ApproverPositionId, command.TargetType, command.TargetId, tagIds);
        if (validation is not null) return validation;

        var now = DateTime.UtcNow;
        var risk = new Risk(
            Id: Guid.NewGuid(),
            Title: command.Title,
            Description: command.Description,
            Impact: command.Impact,
            Likelihood: command.Likelihood,
            Status: command.Status,
            OwnerPositionId: command.OwnerPositionId,
            ApproverPositionId: command.ApproverPositionId,
            TargetType: command.TargetType,
            TargetId: command.TargetId,
            Mitigation: command.Mitigation,
            ReviewDate: command.ReviewDate,
            ApprovalDate: command.ApprovalDate,
            TagIds: tagIds,
            Notes: command.Notes,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { Risks = s.Risks.Append(risk).ToList() });

        // Log audit
        var auditLog = AuditHelper.CreateLog(
            AuditAction.RiskCreated,
            AuditArea.Risk,
            _currentUser.UserName,
            _currentUser.UserId,
            risk.Id,
            risk
        );
        await _auditService.LogAsync(auditLog);

        return Result<Risk>.Success(risk);
    }

    public async Task<Result<Risk>> UpdateRiskAsync(UpdateRisk command)
    {
        var store = await _fuseStore.GetAsync();
        var tagIds = command.TagIds ?? new HashSet<Guid>();
        var existing = store.Risks.FirstOrDefault(r => r.Id == command.Id);
        if (existing is null)
            return Result<Risk>.Failure($"Risk with ID '{command.Id}' not found.", ErrorType.NotFound);

        var validation = await ValidateRiskCommand(command.OwnerPositionId, command.ApproverPositionId, command.TargetType, command.TargetId, tagIds);
        if (validation is not null) return validation;

        var updated = existing with
        {
            Title = command.Title,
            Description = command.Description,
            Impact = command.Impact,
            Likelihood = command.Likelihood,
            Status = command.Status,
            OwnerPositionId = command.OwnerPositionId,
            ApproverPositionId = command.ApproverPositionId,
            TargetType = command.TargetType,
            TargetId = command.TargetId,
            Mitigation = command.Mitigation,
            ReviewDate = command.ReviewDate,
            ApprovalDate = command.ApprovalDate,
            TagIds = tagIds,
            Notes = command.Notes,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { Risks = s.Risks.Select(x => x.Id == command.Id ? updated : x).ToList() });

        // Log audit
        var auditLog = AuditHelper.CreateLog(
            AuditAction.RiskUpdated,
            AuditArea.Risk,
            _currentUser.UserName,
            _currentUser.UserId,
            updated.Id,
            updated
        );
        await _auditService.LogAsync(auditLog);

        return Result<Risk>.Success(updated);
    }

    public async Task<Result> DeleteRiskAsync(DeleteRisk command)
    {
        var store = await _fuseStore.GetAsync();
        if (!store.Risks.Any(r => r.Id == command.Id))
            return Result.Failure($"Risk with ID '{command.Id}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s => s with { Risks = s.Risks.Where(x => x.Id != command.Id).ToList() });

        // Log audit
        var auditLog = AuditHelper.CreateLog(
            AuditAction.RiskDeleted,
            AuditArea.Risk,
            _currentUser.UserName,
            _currentUser.UserId,
            command.Id,
            null
        );
        await _auditService.LogAsync(auditLog);

        return Result.Success();
    }

    private async Task<Result<Risk>?> ValidateRiskCommand(Guid ownerPositionId, Guid? approverPositionId, string targetType, Guid targetId, HashSet<Guid> tagIds)
    {
        var store = await _fuseStore.GetAsync();

        // Validate title is not empty (done by caller or controller)
        
        // Validate owner position exists
        if (!store.Positions.Any(p => p.Id == ownerPositionId))
            return Result<Risk>.Failure($"Owner position with ID '{ownerPositionId}' not found.", ErrorType.Validation);

        // Validate approver position exists if provided
        if (approverPositionId.HasValue && !store.Positions.Any(p => p.Id == approverPositionId.Value))
            return Result<Risk>.Failure($"Approver position with ID '{approverPositionId}' not found.", ErrorType.Validation);

        // Validate target exists based on target type
        var targetExists = targetType switch
        {
            "Application" => store.Applications.Any(a => a.Id == targetId),
            "ApplicationInstance" => store.Applications.Any(a => a.Instances.Any(i => i.Id == targetId)),
            "Dependency" => store.Applications.Any(a => a.Instances.Any(i => i.Dependencies.Any(d => d.Id == targetId))),
            "DataStore" => store.DataStores.Any(d => d.Id == targetId),
            "Account" => store.Accounts.Any(a => a.Id == targetId),
            "ExternalResource" => store.ExternalResources.Any(e => e.Id == targetId),
            _ => false
        };

        if (!targetExists)
            return Result<Risk>.Failure($"Target {targetType} with ID '{targetId}' not found.", ErrorType.Validation);

        // Validate tags exist
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Risk>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        return null;
    }
}
