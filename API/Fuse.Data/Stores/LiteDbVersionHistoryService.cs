using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using LiteDB;

namespace Fuse.Data.Stores;

/// <summary>
/// LiteDB-based implementation of IVersionHistoryService
/// </summary>
public sealed class LiteDbVersionHistoryService : IVersionHistoryService, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<EntityVersion> _versions;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private const int DefaultKeepCount = 30;

    public LiteDbVersionHistoryService(string dataDirectory)
    {
        var dbPath = Path.Combine(dataDirectory, "versions.db");
        _db = new LiteDatabase(dbPath);
        _versions = _db.GetCollection<EntityVersion>("versions");
        
        // Create indexes for efficient querying
        _versions.EnsureIndex(x => x.EntityId);
        _versions.EnsureIndex(x => x.EntityType);
        _versions.EnsureIndex(x => x.Timestamp);
        _versions.EnsureIndex(x => x.Version);
        _versions.EnsureIndex(x => x.UserId);
        
        // Single-field indexes above already cover the current query patterns.
    }

    public async Task SaveVersionAsync(EntityVersion version, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await Task.Run(() =>
            {
                _versions.Insert(version);
                
                // Auto-prune old versions to keep only the most recent ones
                PruneOldVersionsInternal(version.EntityId, version.EntityType, DefaultKeepCount);
            }, ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<EntityVersion>> GetVersionsAsync(
        Guid entityId, 
        EntityType entityType, 
        int? limit = null, 
        CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                var query = _versions
                    .Query()
                    .Where(x => x.EntityId == entityId && x.EntityType == entityType)
                    .OrderByDescending(x => x.Version);

                if (limit.HasValue && limit.Value > 0)
                    return (IReadOnlyList<EntityVersion>)query.Limit(limit.Value).ToList();

                return (IReadOnlyList<EntityVersion>)query.ToList();
            }, ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<VersionHistoryResult> QueryAsync(VersionHistoryQuery query, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                var q = _versions.Query();

                // Apply filters
                if (query.EntityId.HasValue)
                    q = q.Where(x => x.EntityId == query.EntityId.Value);
                
                if (query.EntityType.HasValue)
                    q = q.Where(x => x.EntityType == query.EntityType.Value);
                
                if (query.StartTime.HasValue)
                    q = q.Where(x => x.Timestamp >= query.StartTime.Value);
                
                if (query.EndTime.HasValue)
                    q = q.Where(x => x.Timestamp <= query.EndTime.Value);
                
                if (query.UserId.HasValue)
                    q = q.Where(x => x.UserId == query.UserId.Value);
                
                if (!string.IsNullOrWhiteSpace(query.UserName))
                    q = q.Where(x => x.UserName == query.UserName);

                // Get total count
                var totalCount = q.Count();

                // Apply ordering (newest first)
                q = q.OrderByDescending(x => x.Timestamp);

                // Apply pagination
                var skip = (query.Page - 1) * query.PageSize;
                var versions = q.Skip(skip).Limit(query.PageSize).ToList();

                return new VersionHistoryResult(versions, totalCount, query.Page, query.PageSize);
            }, ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<EntityVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            return await Task.Run(() => _versions.FindById(versionId), ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<EntityVersion?> GetLatestVersionAsync(Guid entityId, EntityType entityType, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                return _versions
                    .Query()
                    .Where(x => x.EntityId == entityId && x.EntityType == entityType)
                    .OrderByDescending(x => x.Version)
                    .Limit(1)
                    .FirstOrDefault();
            }, ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task PruneOldVersionsAsync(Guid entityId, EntityType entityType, int keepCount, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await Task.Run(() => PruneOldVersionsInternal(entityId, entityType, keepCount), ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>
    /// Internal method to prune old versions (must be called within mutex)
    /// </summary>
    private void PruneOldVersionsInternal(Guid entityId, EntityType entityType, int keepCount)
    {
        // Get all versions for this entity, ordered by version descending
        var allVersions = _versions
            .Query()
            .Where(x => x.EntityId == entityId && x.EntityType == entityType)
            .OrderByDescending(x => x.Version)
            .ToList();

        // If we have more than keepCount, delete the older ones
        if (allVersions.Count > keepCount)
        {
            var versionsToDelete = allVersions.Skip(keepCount).ToList();
            foreach (var version in versionsToDelete)
            {
                _versions.Delete(version.Id);
            }
        }
    }

    public void Dispose()
    {
        _db?.Dispose();
        _mutex?.Dispose();
    }
}
