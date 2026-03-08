using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

/// <summary>
/// Service for managing entity version history
/// </summary>
public interface IVersionHistoryService
{
    /// <summary>
    /// Save a new version of an entity
    /// </summary>
    Task SaveVersionAsync(EntityVersion version, CancellationToken ct = default);
    
    /// <summary>
    /// Get version history for a specific entity with optional limit
    /// </summary>
    Task<IReadOnlyList<EntityVersion>> GetVersionsAsync(
        Guid entityId, 
        EntityType entityType, 
        int? limit = null, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Query version history with filtering and pagination
    /// </summary>
    Task<VersionHistoryResult> QueryAsync(VersionHistoryQuery query, CancellationToken ct = default);
    
    /// <summary>
    /// Get a specific version by ID
    /// </summary>
    Task<EntityVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken ct = default);
    
    /// <summary>
    /// Get the latest version for a specific entity
    /// </summary>
    Task<EntityVersion?> GetLatestVersionAsync(Guid entityId, EntityType entityType, CancellationToken ct = default);
    
    /// <summary>
    /// Prune old versions for a specific entity, keeping only the most recent versions up to the specified count
    /// </summary>
    Task PruneOldVersionsAsync(Guid entityId, EntityType entityType, int keepCount, CancellationToken ct = default);
}
