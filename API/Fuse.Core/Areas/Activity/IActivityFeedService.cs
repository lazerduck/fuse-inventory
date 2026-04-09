using Fuse.Core.Models;

namespace Fuse.Core.Areas.Activity;

public interface IActivityFeedService
{
    Task<ActivityFeedResult> QueryAsync(VersionHistoryQuery query, CancellationToken ct = default);
    Task<ActivityFeedResult> QueryByEntityAsync(EntityType entityType, Guid entityId, int page = 1, int pageSize = 50, CancellationToken ct = default);
}
