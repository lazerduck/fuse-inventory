using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public sealed class UndoService : IUndoService
{
    private readonly IVersionHistoryService _versionHistoryService;
    private readonly IFuseStore _fuseStore;
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;

    public UndoService(
        IVersionHistoryService versionHistoryService,
        IFuseStore fuseStore,
        IAuditService auditService,
        ICurrentUser currentUser)
    {
        _versionHistoryService = versionHistoryService;
        _fuseStore = fuseStore;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    public async Task<Result<UndoChangeResult>> UndoChangeAsync(Guid versionId, CancellationToken ct = default)
    {
        var target = await _versionHistoryService.GetVersionByIdAsync(versionId, ct);
        if (target is null)
            return Result<UndoChangeResult>.Failure($"Version '{versionId}' was not found.", ErrorType.NotFound);

        var versions = await _versionHistoryService.GetVersionsAsync(target.EntityId, target.EntityType, null, ct);
        var ordered = versions.OrderByDescending(v => v.Version).ToList();
        var index = ordered.FindIndex(v => v.Id == versionId);
        if (index < 0)
            return Result<UndoChangeResult>.Failure($"Version '{versionId}' was not found in entity history.", ErrorType.NotFound);

        var previous = index + 1 < ordered.Count ? ordered[index + 1] : null;

        // Undo rule:
        // - If target is a delete (snapshot null), restore previous snapshot.
        // - Otherwise restore previous snapshot; if none exists, remove the entity.
        string? restoredSnapshot = target.EntitySnapshot is null
            ? previous?.EntitySnapshot
            : previous?.EntitySnapshot;

        if (target.EntitySnapshot is null && previous is null)
            return Result<UndoChangeResult>.Failure("Cannot undo this deletion because no prior version exists.", ErrorType.Validation);

        object? restoredEntity = null;
        if (restoredSnapshot is not null)
        {
            restoredEntity = EntityExtractor.DeserializeEntity(restoredSnapshot, target.EntityType);
            if (restoredEntity is null)
                return Result<UndoChangeResult>.Failure("Unable to deserialize the previous entity version.", ErrorType.ServerError);
        }

        try
        {
            await _fuseStore.UpdateAsync(s => EntityExtractor.SetEntity(s, target.EntityType, target.EntityId, restoredEntity), ct);
        }
        catch (InvalidOperationException ex)
        {
            return Result<UndoChangeResult>.Failure($"Undo failed validation: {ex.Message}", ErrorType.Validation);
        }

        var auditLog = AuditHelper.CreateLog(
            AuditAction.ChangeReverted,
            EntityAuditMapper.ToAuditArea(target.EntityType),
            _currentUser.UserName,
            _currentUser.UserId,
            target.EntityId,
            new
            {
                VersionId = target.Id,
                target.EntityType,
                target.EntityId,
                RestoredFromVersion = previous?.Version,
                RevertedVersion = target.Version
            });
        await _auditService.LogAsync(auditLog, ct);

        var result = new UndoChangeResult(
            VersionId: target.Id,
            EntityId: target.EntityId,
            EntityType: target.EntityType,
            RestoredFromVersion: previous?.Version ?? 0,
            Message: previous is null
                ? "Entity reverted by removing its first recorded version."
                : $"Entity reverted to version {previous.Version}."
        );

        return Result<UndoChangeResult>.Success(result);
    }
}
