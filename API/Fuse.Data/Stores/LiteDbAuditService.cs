using Fuse.Core.Interfaces;
using Fuse.Core.Areas.Audit;
using Fuse.Core.Models;
using LiteDB;

namespace Fuse.Data.Stores;

/// <summary>
/// LiteDB-based implementation of IAuditService
/// </summary>
public sealed class LiteDbAuditService : IAuditService, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<AuditLog> _auditLogs;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly IFuseStore _fuseStore;

    public LiteDbAuditService(IFuseStore fuseStore, string dataDirectory)
    {
        _fuseStore = fuseStore;
        var dbPath = Path.Combine(dataDirectory, "audit.db");
        _db = new LiteDatabase(dbPath);
        _auditLogs = _db.GetCollection<AuditLog>("auditlogs");
        
        // Create indexes for efficient querying
        _auditLogs.EnsureIndex(x => x.Timestamp);
        _auditLogs.EnsureIndex(x => x.Action);
        _auditLogs.EnsureIndex(x => x.Area);
        _auditLogs.EnsureIndex(x => x.UserName);
        _auditLogs.EnsureIndex(x => x.EntityId);
    }

    public async Task LogAsync(AuditLog log, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await Task.Run(() => _auditLogs.Insert(log), ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<AuditLogResult> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                var q = _auditLogs.Query();

                // Apply filters
                if (query.StartTime.HasValue)
                    q = q.Where(x => x.Timestamp >= query.StartTime.Value);
                
                if (query.EndTime.HasValue)
                    q = q.Where(x => x.Timestamp <= query.EndTime.Value);
                
                if (query.Action.HasValue)
                    q = q.Where(x => x.Action == query.Action.Value);
                
                if (query.Area.HasValue)
                    q = q.Where(x => x.Area == query.Area.Value);
                
                if (!string.IsNullOrWhiteSpace(query.UserName))
                    q = q.Where(x => x.UserName == query.UserName);
                
                if (query.EntityId.HasValue)
                    q = q.Where(x => x.EntityId == query.EntityId.Value);
                
                if (!string.IsNullOrWhiteSpace(query.SearchText))
                {
                    var searchText = query.SearchText;
                    q = q.Where(x => x.ChangeDetails != null && x.ChangeDetails.Contains(searchText));
                }

                // Get total count
                var totalCount = q.Count();

                // Apply ordering (newest first)
                q = q.OrderByDescending(x => x.Timestamp);

                // Apply pagination
                var skip = (query.Page - 1) * query.PageSize;
                var logs = q.Skip(skip).Limit(query.PageSize).ToList();

                return new AuditLogResult(logs, totalCount, query.Page, query.PageSize);
            }, ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            return await Task.Run(() => _auditLogs.FindById(id), ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task DeleteOlderThanAsync(DateTime date, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await Task.Run(() => _auditLogs.DeleteMany(x => x.Timestamp < date), ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>
    /// Deletes audit logs older than the configured retention period.
    /// If AuditLogDaysToKeep is null or 0, no cleanup is performed (unlimited).
    /// </summary>
    public async Task CleanupOldAuditLogsAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var daysToKeep = await _fuseStore.GetAsync(
                s => s.AppSettings?.AuditLogDaysToKeep ?? 0, ct);

            if (daysToKeep <= 0)
                return; // unlimited — nothing to do

            var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
            await Task.Run(() => _auditLogs.DeleteMany(x => x.Timestamp < cutoff), ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public void Dispose()
    {
        _db?.Dispose();
        _mutex?.Dispose();
    }
}
