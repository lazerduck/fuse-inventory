using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class ResponsibilityAssignmentService : IResponsibilityAssignmentService
{
    private readonly IFuseStore _fuseStore;
    private readonly IAuditService _auditService;

    public ResponsibilityAssignmentService(
        IFuseStore fuseStore,
        IAuditService auditService)
    {
        _fuseStore = fuseStore;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<ResponsibilityAssignment>> GetResponsibilityAssignmentsAsync()
    {
        var store = await _fuseStore.GetAsync();
        return store.ResponsibilityAssignments;
    }

    public async Task<IReadOnlyList<ResponsibilityAssignment>> GetResponsibilityAssignmentsByApplicationIdAsync(Guid applicationId)
    {
        var store = await _fuseStore.GetAsync();
        return store.ResponsibilityAssignments
            .Where(ra => ra.ApplicationId == applicationId)
            .ToList();
    }

    public async Task<ResponsibilityAssignment?> GetResponsibilityAssignmentByIdAsync(Guid id)
    {
        var store = await _fuseStore.GetAsync();
        return store.ResponsibilityAssignments.FirstOrDefault(ra => ra.Id == id);
    }

    public async Task<Result<ResponsibilityAssignment>> CreateResponsibilityAssignmentAsync(CreateResponsibilityAssignment command, ICurrentUser currentUser)
    {
        var store = await _fuseStore.GetAsync();

        // Validate position exists
        if (!store.Positions.Any(p => p.Id == command.PositionId))
            return Result<ResponsibilityAssignment>.Failure($"Position with ID '{command.PositionId}' not found.", ErrorType.Validation);

        // Validate responsibility type exists
        if (!store.ResponsibilityTypes.Any(rt => rt.Id == command.ResponsibilityTypeId))
            return Result<ResponsibilityAssignment>.Failure($"Responsibility type with ID '{command.ResponsibilityTypeId}' not found.", ErrorType.Validation);

        // Validate application exists
        if (!store.Applications.Any(a => a.Id == command.ApplicationId))
            return Result<ResponsibilityAssignment>.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.Validation);

        // Validate environment if scope is Environment
        if (command.Scope == ResponsibilityScope.Environment)
        {
            if (command.EnvironmentId == null)
                return Result<ResponsibilityAssignment>.Failure("Environment ID is required when scope is Environment.", ErrorType.Validation);

            if (!store.Environments.Any(e => e.Id == command.EnvironmentId))
                return Result<ResponsibilityAssignment>.Failure($"Environment with ID '{command.EnvironmentId}' not found.", ErrorType.Validation);
        }

        var now = DateTime.UtcNow;
        var assignment = new ResponsibilityAssignment(
            Id: Guid.NewGuid(),
            PositionId: command.PositionId,
            ResponsibilityTypeId: command.ResponsibilityTypeId,
            ApplicationId: command.ApplicationId,
            Scope: command.Scope,
            EnvironmentId: command.EnvironmentId,
            Notes: command.Notes,
            Primary: command.Primary,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { ResponsibilityAssignments = s.ResponsibilityAssignments.Append(assignment).ToList() });

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ResponsibilityAssignmentCreated,
            AuditArea.ResponsibilityAssignment,
            currentUser.UserName,
            currentUser.UserId,
            assignment.Id,
            new
            {
                assignment.Id,
                assignment.PositionId,
                assignment.ResponsibilityTypeId,
                assignment.ApplicationId,
                assignment.Scope,
                assignment.EnvironmentId,
                assignment.Primary
            }
        );
        await _auditService.LogAsync(auditLog);

        return Result<ResponsibilityAssignment>.Success(assignment);
    }

    public async Task<Result<ResponsibilityAssignment>> UpdateResponsibilityAssignmentAsync(UpdateResponsibilityAssignment command, ICurrentUser currentUser)
    {
        var store = await _fuseStore.GetAsync();
        var existing = store.ResponsibilityAssignments.FirstOrDefault(ra => ra.Id == command.Id);
        if (existing is null)
            return Result<ResponsibilityAssignment>.Failure($"Responsibility assignment with ID '{command.Id}' not found.", ErrorType.NotFound);

        // Validate position exists
        if (!store.Positions.Any(p => p.Id == command.PositionId))
            return Result<ResponsibilityAssignment>.Failure($"Position with ID '{command.PositionId}' not found.", ErrorType.Validation);

        // Validate responsibility type exists
        if (!store.ResponsibilityTypes.Any(rt => rt.Id == command.ResponsibilityTypeId))
            return Result<ResponsibilityAssignment>.Failure($"Responsibility type with ID '{command.ResponsibilityTypeId}' not found.", ErrorType.Validation);

        // Validate application exists
        if (!store.Applications.Any(a => a.Id == command.ApplicationId))
            return Result<ResponsibilityAssignment>.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.Validation);

        // Validate environment if scope is Environment
        if (command.Scope == ResponsibilityScope.Environment)
        {
            if (command.EnvironmentId == null)
                return Result<ResponsibilityAssignment>.Failure("Environment ID is required when scope is Environment.", ErrorType.Validation);

            if (!store.Environments.Any(e => e.Id == command.EnvironmentId))
                return Result<ResponsibilityAssignment>.Failure($"Environment with ID '{command.EnvironmentId}' not found.", ErrorType.Validation);
        }

        var updated = existing with
        {
            PositionId = command.PositionId,
            ResponsibilityTypeId = command.ResponsibilityTypeId,
            ApplicationId = command.ApplicationId,
            Scope = command.Scope,
            EnvironmentId = command.EnvironmentId,
            Notes = command.Notes,
            Primary = command.Primary,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { ResponsibilityAssignments = s.ResponsibilityAssignments.Select(ra => ra.Id == command.Id ? updated : ra).ToList() });

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ResponsibilityAssignmentUpdated,
            AuditArea.ResponsibilityAssignment,
            currentUser.UserName,
            currentUser.UserId,
            updated.Id,
            new
            {
                updated.Id,
                updated.PositionId,
                updated.ResponsibilityTypeId,
                updated.ApplicationId,
                updated.Scope,
                updated.EnvironmentId,
                updated.Primary
            }
        );
        await _auditService.LogAsync(auditLog);

        return Result<ResponsibilityAssignment>.Success(updated);
    }

    public async Task<Result> DeleteResponsibilityAssignmentAsync(DeleteResponsibilityAssignment command, ICurrentUser currentUser)
    {
        var store = await _fuseStore.GetAsync();
        var existing = store.ResponsibilityAssignments.FirstOrDefault(ra => ra.Id == command.Id);
        if (existing is null)
            return Result.Failure($"Responsibility assignment with ID '{command.Id}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s => s with { ResponsibilityAssignments = s.ResponsibilityAssignments.Where(ra => ra.Id != command.Id).ToList() });

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ResponsibilityAssignmentDeleted,
            AuditArea.ResponsibilityAssignment,
            currentUser.UserName,
            currentUser.UserId,
            command.Id,
            new
            {
                Id = command.Id,
                existing.PositionId,
                existing.ResponsibilityTypeId,
                existing.ApplicationId
            }
        );
        await _auditService.LogAsync(auditLog);

        return Result.Success();
    }
}
