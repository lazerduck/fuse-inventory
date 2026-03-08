namespace Fuse.Core.Models;

/// <summary>
/// Query parameters for retrieving version history
/// </summary>
public record VersionHistoryQuery
{
    /// <summary>
    /// Filter by entity ID (optional)
    /// </summary>
    public Guid? EntityId { get; init; }
    
    /// <summary>
    /// Filter by entity type (optional)
    /// </summary>
    public EntityType? EntityType { get; init; }
    
    /// <summary>
    /// Filter by minimum timestamp (optional)
    /// </summary>
    public DateTime? StartTime { get; init; }
    
    /// <summary>
    /// Filter by maximum timestamp (optional)
    /// </summary>
    public DateTime? EndTime { get; init; }
    
    /// <summary>
    /// Filter by user ID (optional)
    /// </summary>
    public Guid? UserId { get; init; }
    
    /// <summary>
    /// Filter by username (optional)
    /// </summary>
    public string? UserName { get; init; }
    
    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int Page { get; init; } = 1;
    
    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; init; } = 50;
    
    public VersionHistoryQuery()
    {
    }
    
    public VersionHistoryQuery(
        Guid? entityId = null,
        EntityType? entityType = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        Guid? userId = null,
        string? userName = null,
        int page = 1,
        int pageSize = 50)
    {
        EntityId = entityId;
        EntityType = entityType;
        StartTime = startTime;
        EndTime = endTime;
        UserId = userId;
        UserName = userName;
        Page = page;
        PageSize = pageSize;
    }
}

/// <summary>
/// Result of a version history query with pagination info
/// </summary>
public record VersionHistoryResult(
    IReadOnlyList<EntityVersion> Versions,
    int TotalCount,
    int Page,
    int PageSize);
