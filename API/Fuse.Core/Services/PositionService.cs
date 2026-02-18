using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class PositionService : IPositionService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public PositionService(
        IFuseStore fuseStore,
        ITagService tagService,
        IAuditService auditService,
        ICurrentUser currentUser)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<Position>> GetPositionsAsync()
        => (await _fuseStore.GetAsync()).Positions;

    public async Task<Position?> GetPositionByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).Positions.FirstOrDefault(p => p.Id == id);

    public async Task<Result<Position>> CreatePositionAsync(CreatePosition command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<Position>.Failure("Position name cannot be empty.", ErrorType.Validation);

        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Position>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        var store = await _fuseStore.GetAsync();
        if (store.Positions.Any(p => string.Equals(p.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<Position>.Failure($"Position with name '{command.Name}' already exists.", ErrorType.Conflict);

        var now = DateTime.UtcNow;
        var position = new Position(
            Id: Guid.NewGuid(),
            Name: command.Name,
            Description: command.Description,
            TagIds: tagIds,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { Positions = s.Positions.Append(position).ToList() });

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.PositionCreated,
            AuditArea.Position,
            _currentUser.UserName,
            _currentUser.UserId,
            position.Id,
            new { position.Id, position.Name, position.Description }
        );
        await _auditService.LogAsync(auditLog);

        return Result<Position>.Success(position);
    }

    public async Task<Result<Position>> UpdatePositionAsync(UpdatePosition command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<Position>.Failure("Position name cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        var existing = store.Positions.FirstOrDefault(p => p.Id == command.Id);
        if (existing is null)
            return Result<Position>.Failure($"Position with ID '{command.Id}' not found.", ErrorType.NotFound);

        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Position>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        // Check for name conflicts (excluding current position)
        if (store.Positions.Any(p => p.Id != command.Id && string.Equals(p.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<Position>.Failure($"Position with name '{command.Name}' already exists.", ErrorType.Conflict);

        var updated = existing with
        {
            Name = command.Name,
            Description = command.Description,
            TagIds = tagIds,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { Positions = s.Positions.Select(p => p.Id == command.Id ? updated : p).ToList() });

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.PositionUpdated,
            AuditArea.Position,
            _currentUser.UserName,
            _currentUser.UserId,
            updated.Id,
            new { updated.Id, updated.Name, updated.Description }
        );
        await _auditService.LogAsync(auditLog);

        return Result<Position>.Success(updated);
    }

    public async Task<Result> DeletePositionAsync(DeletePosition command)
    {
        var store = await _fuseStore.GetAsync();
        var existing = store.Positions.FirstOrDefault(p => p.Id == command.Id);
        if (existing is null)
            return Result.Failure($"Position with ID '{command.Id}' not found.", ErrorType.NotFound);

        // Check if position is referenced by any responsibility assignments
        if (store.ResponsibilityAssignments.Any(ra => ra.PositionId == command.Id))
            return Result.Failure($"Position '{existing.Name}' is referenced by one or more responsibility assignments and cannot be deleted.", ErrorType.Conflict);

        await _fuseStore.UpdateAsync(s => s with { Positions = s.Positions.Where(p => p.Id != command.Id).ToList() });

        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.PositionDeleted,
            AuditArea.Position,
            _currentUser.UserName,
            _currentUser.UserId,
            command.Id,
            new { Id = command.Id, existing.Name }
        );
        await _auditService.LogAsync(auditLog);

        return Result.Success();
    }
}
