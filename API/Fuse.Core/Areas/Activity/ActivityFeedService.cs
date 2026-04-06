using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Activity;

public sealed class ActivityFeedService : IActivityFeedService
{
    private readonly IVersionHistoryService _versionHistoryService;

    public ActivityFeedService(IVersionHistoryService versionHistoryService)
    {
        _versionHistoryService = versionHistoryService;
    }

    public async Task<ActivityFeedResult> QueryAsync(VersionHistoryQuery query, CancellationToken ct = default)
    {
        var history = await _versionHistoryService.QueryAsync(query, ct);
        var canUndoByVersionId = await BuildUndoLookupAsync(history.Versions, ct);

        var items = history.Versions
            .Select(v => new ActivityFeedItem(
                VersionId: v.Id,
                EntityId: v.EntityId,
                EntityType: v.EntityType,
                Version: v.Version,
                Timestamp: v.Timestamp,
                Action: v.Action,
                Area: EntityAuditMapper.ToAuditArea(v.EntityType),
                UserName: v.UserName,
                UserId: v.UserId,
                CanUndo: canUndoByVersionId.TryGetValue(v.Id, out var canUndo) && canUndo,
                ChangeDescription: v.ChangeDescription))
            .ToList();

        return new ActivityFeedResult(items, history.TotalCount, history.Page, history.PageSize);
    }

    public Task<ActivityFeedResult> QueryByEntityAsync(EntityType entityType, Guid entityId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var query = new VersionHistoryQuery(entityId: entityId, entityType: entityType, page: page, pageSize: pageSize);
        return QueryAsync(query, ct);
    }

    private async Task<Dictionary<Guid, bool>> BuildUndoLookupAsync(IReadOnlyList<EntityVersion> versions, CancellationToken ct)
    {
        var lookup = new Dictionary<Guid, bool>();
        var groups = versions.GroupBy(v => (v.EntityId, v.EntityType));

        foreach (var group in groups)
        {
            var entityVersions = await _versionHistoryService.GetVersionsAsync(group.Key.EntityId, group.Key.EntityType, null, ct);
            var ordered = entityVersions.OrderByDescending(v => v.Version).ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                var version = ordered[i];
                var hasPrevious = i + 1 < ordered.Count;

                // A delete requires a previous version to restore. Other changes can always be reverted.
                lookup[version.Id] = version.EntitySnapshot is null ? hasPrevious : true;
            }
        }

        return lookup;
    }
}
