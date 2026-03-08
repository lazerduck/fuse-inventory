namespace Fuse.Core.Models;

public record ActivityFeedItem(
    Guid VersionId,
    Guid EntityId,
    EntityType EntityType,
    int Version,
    DateTime Timestamp,
    AuditAction Action,
    AuditArea Area,
    string UserName,
    Guid? UserId,
    bool CanUndo,
    string? ChangeDescription
);

public record ActivityFeedResult(
    IReadOnlyList<ActivityFeedItem> Items,
    int TotalCount,
    int Page,
    int PageSize
);
