using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class ResponsibilityTypeService : IResponsibilityTypeService
{
    private readonly IFuseStore _fuseStore;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public ResponsibilityTypeService(
        IFuseStore fuseStore,
        IAuditService auditService,
        ICurrentUser currentUser)
    {
        _fuseStore = fuseStore;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ResponsibilityType>> GetResponsibilityTypesAsync()
        => (await _fuseStore.GetAsync()).ResponsibilityTypes;

    public async Task<ResponsibilityType?> GetResponsibilityTypeByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).ResponsibilityTypes.FirstOrDefault(rt => rt.Id == id);

    public async Task<Result<ResponsibilityType>> CreateResponsibilityTypeAsync(CreateResponsibilityType command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<ResponsibilityType>.Failure("Responsibility type name cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        if (store.ResponsibilityTypes.Any(rt => string.Equals(rt.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<ResponsibilityType>.Failure($"Responsibility type with name '{command.Name}' already exists.", ErrorType.Conflict);

        var now = DateTime.UtcNow;
        var responsibilityType = new ResponsibilityType(
            Id: Guid.NewGuid(),
            Name: command.Name,
            Description: command.Description,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { ResponsibilityTypes = s.ResponsibilityTypes.Append(responsibilityType).ToList() });

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ResponsibilityTypeCreated,
            AuditArea.ResponsibilityType,
            _currentUser.UserName,
            _currentUser.UserId,
            responsibilityType.Id,
            new { responsibilityType.Id, responsibilityType.Name, responsibilityType.Description }
        );
        await _auditService.LogAsync(auditLog);

        return Result<ResponsibilityType>.Success(responsibilityType);
    }

    public async Task<Result<ResponsibilityType>> UpdateResponsibilityTypeAsync(UpdateResponsibilityType command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<ResponsibilityType>.Failure("Responsibility type name cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        var existing = store.ResponsibilityTypes.FirstOrDefault(rt => rt.Id == command.Id);
        if (existing is null)
            return Result<ResponsibilityType>.Failure($"Responsibility type with ID '{command.Id}' not found.", ErrorType.NotFound);

        // Check for name conflicts (excluding current responsibility type)
        if (store.ResponsibilityTypes.Any(rt => rt.Id != command.Id && string.Equals(rt.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<ResponsibilityType>.Failure($"Responsibility type with name '{command.Name}' already exists.", ErrorType.Conflict);

        var updated = existing with
        {
            Name = command.Name,
            Description = command.Description,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { ResponsibilityTypes = s.ResponsibilityTypes.Select(rt => rt.Id == command.Id ? updated : rt).ToList() });

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ResponsibilityTypeUpdated,
            AuditArea.ResponsibilityType,
            _currentUser.UserName,
            _currentUser.UserId,
            updated.Id,
            new { updated.Id, updated.Name, updated.Description }
        );
        await _auditService.LogAsync(auditLog);

        return Result<ResponsibilityType>.Success(updated);
    }

    public async Task<Result> DeleteResponsibilityTypeAsync(DeleteResponsibilityType command)
    {
        var store = await _fuseStore.GetAsync();
        var existing = store.ResponsibilityTypes.FirstOrDefault(rt => rt.Id == command.Id);
        if (existing is null)
            return Result.Failure($"Responsibility type with ID '{command.Id}' not found.", ErrorType.NotFound);

        // Check if responsibility type is referenced by any responsibility assignments
        if (store.ResponsibilityAssignments.Any(ra => ra.ResponsibilityTypeId == command.Id))
            return Result.Failure($"Responsibility type '{existing.Name}' is referenced by one or more responsibility assignments and cannot be deleted.", ErrorType.Conflict);

        await _fuseStore.UpdateAsync(s => s with { ResponsibilityTypes = s.ResponsibilityTypes.Where(rt => rt.Id != command.Id).ToList() });

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ResponsibilityTypeDeleted,
            AuditArea.ResponsibilityType,
            _currentUser.UserName,
            _currentUser.UserId,
            command.Id,
            new { Id = command.Id, existing.Name }
        );
        await _auditService.LogAsync(auditLog);

        return Result.Success();
    }
}
