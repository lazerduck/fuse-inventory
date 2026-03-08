namespace Fuse.Core.Models;

/// <summary>
/// Represents a versioned snapshot of an entity at a specific point in time
/// </summary>
public record EntityVersion
{
    /// <summary>
    /// Unique identifier for this version entry
    /// </summary>
    public Guid Id { get; init; }
    
    /// <summary>
    /// The ID of the entity that was versioned
    /// </summary>
    public Guid EntityId { get; init; }
    
    /// <summary>
    /// The type of entity
    /// </summary>
    public EntityType EntityType { get; init; }
    
    /// <summary>
    /// Sequential version number for this entity (1, 2, 3, etc.)
    /// </summary>
    public int Version { get; init; }
    
    /// <summary>
    /// JSON snapshot of the entity state at this version
    /// Null indicates the entity was deleted
    /// </summary>
    public string? EntitySnapshot { get; init; }
    
    /// <summary>
    /// Timestamp when this version was created
    /// </summary>
    public DateTime Timestamp { get; init; }
    
    /// <summary>
    /// The audit action that created this version
    /// </summary>
    public AuditAction Action { get; init; }
    
    /// <summary>
    /// The username who made this change
    /// </summary>
    public string UserName { get; init; } = "Anonymous";
    
    /// <summary>
    /// The ID of the user who made this change (null if anonymous)
    /// </summary>
    public Guid? UserId { get; init; }
    
    /// <summary>
    /// Optional description of what changed
    /// </summary>
    public string? ChangeDescription { get; init; }
    
    public EntityVersion()
    {
    }
    
    public EntityVersion(
        Guid id,
        Guid entityId,
        EntityType entityType,
        int version,
        string? entitySnapshot,
        DateTime timestamp,
        AuditAction action,
        string userName,
        Guid? userId,
        string? changeDescription)
    {
        Id = id;
        EntityId = entityId;
        EntityType = entityType;
        Version = version;
        EntitySnapshot = entitySnapshot;
        Timestamp = timestamp;
        Action = action;
        UserName = userName;
        UserId = userId;
        ChangeDescription = changeDescription;
    }
}
