using Fuse.Core.Areas.Logging;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using LiteDB;

namespace Fuse.Data.Stores;

public sealed class LiteDbLogService : ILogService, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<SystemLogEntry> _logs;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly IFuseStore _fuseStore;

    public LiteDbLogService(IFuseStore fuseStore, string dataDirectory)
    {
        _fuseStore = fuseStore;
        var dbPath = Path.Combine(dataDirectory, "logs.db");
        _db = new LiteDatabase(dbPath);
        _logs = _db.GetCollection<SystemLogEntry>("systemlogs");
        _logs.EnsureIndex(x => x.Timestamp);
        _logs.EnsureIndex(x => x.Level);
        _logs.EnsureIndex(x => x.Area);
    }

    public async Task LogAsync(SystemLogEntry entry, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return;

        var settings = await GetSettingsAsync(ct);
        if (!settings.Enabled)
            return;

        if (entry.Level < settings.MinLevel)
            return;

        if (settings.ExcludeAreas?.Any(area => string.Equals(area, entry.Area, StringComparison.OrdinalIgnoreCase)) == true)
            return;

        var storedEntry = entry with
        {
            Id = entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id,
            Timestamp = entry.Timestamp == default ? DateTime.UtcNow : entry.Timestamp,
            Area = string.IsNullOrWhiteSpace(entry.Area) ? "General" : entry.Area.Trim(),
            Message = string.IsNullOrWhiteSpace(entry.Message) ? entry.Level.ToString() : entry.Message.Trim()
        };

        try
        {
            await _mutex.WaitAsync(ct);
            try
            {
                await Task.Run(() => _logs.Insert(storedEntry), ct);
            }
            finally
            {
                _mutex.Release();
            }
        }
        catch
        {
            // Logging must remain best-effort and never break the caller.
        }
    }

    public async Task<SystemLogResult> QueryAsync(SystemLogQuery query, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 50 : query.PageSize;

        await _mutex.WaitAsync(ct);
        try
        {
            var skip = (page - 1) * pageSize;
            var totalCount = await Task.Run(() => ApplyQuery(_logs.Query(), query).Count(), ct);
            var pageLogs = await Task.Run(() => ApplyQuery(_logs.Query(), query)
                .OrderByDescending(x => x.Timestamp)
                .Skip(skip)
                .Limit(pageSize)
                .ToList(), ct);

            return new SystemLogResult(pageLogs, totalCount, page, pageSize);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<SystemLogCounts> GetCountsAsync(SystemLogQuery query, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            return new SystemLogCounts
            {
                Debug = await Task.Run(() => ApplyQuery(_logs.Query(), query).Where(x => x.Level == LogLevel.Debug).Count(), ct),
                Info = await Task.Run(() => ApplyQuery(_logs.Query(), query).Where(x => x.Level == LogLevel.Info).Count(), ct),
                Warning = await Task.Run(() => ApplyQuery(_logs.Query(), query).Where(x => x.Level == LogLevel.Warning).Count(), ct),
                Error = await Task.Run(() => ApplyQuery(_logs.Query(), query).Where(x => x.Level == LogLevel.Error).Count(), ct)
            };
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetAreasAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var areas = await Task.Run(() => _logs.Query()
                .Select(x => x.Area)
                .ToList()
                .Where(area => !string.IsNullOrWhiteSpace(area))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(area => area, StringComparer.OrdinalIgnoreCase)
                .ToList(), ct);

            return areas;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            return await Task.Run(() => _logs.Count(), ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task CleanupOldLogsAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        if (settings.DaysToKeep is null or <= 0)
            return;

        var cutoff = DateTime.UtcNow.AddDays(-settings.DaysToKeep.Value);

        await _mutex.WaitAsync(ct);
        try
        {
            await Task.Run(() => _logs.DeleteMany(x => x.Timestamp < cutoff), ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<LoggingSettings> GetSettingsAsync(CancellationToken ct)
    {
        var snapshot = await _fuseStore.GetAsync(ct);
        var logging = snapshot.AppSettings.Logging;

        return new LoggingSettings
        {
            Enabled = logging?.Enabled ?? LoggingSettings.Default.Enabled,
            MinLevel = logging?.MinLevel ?? LoggingSettings.Default.MinLevel,
            DaysToKeep = logging?.DaysToKeep,
            ExcludeAreas = logging?.ExcludeAreas?
                .Where(area => !string.IsNullOrWhiteSpace(area))
                .Select(area => area.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static ILiteQueryable<SystemLogEntry> ApplyQuery(ILiteQueryable<SystemLogEntry> queryable, SystemLogQuery query)
    {
        var filtered = queryable;

        if (query.MinLevel.HasValue)
            filtered = filtered.Where(x => x.Level >= query.MinLevel.Value);

        if (!string.IsNullOrWhiteSpace(query.Area))
            filtered = filtered.Where(x => x.Area == query.Area);

        if (query.StartTime.HasValue)
            filtered = filtered.Where(x => x.Timestamp >= query.StartTime.Value);

        if (query.EndTime.HasValue)
            filtered = filtered.Where(x => x.Timestamp <= query.EndTime.Value);

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText.Trim();
            filtered = filtered.Where(x =>
                x.Message != null && x.Message.Contains(searchText)
                || x.Details != null && x.Details.Contains(searchText)
                || x.Exception != null && x.Exception.Contains(searchText)
                || x.Area != null && x.Area.Contains(searchText));
        }

        return filtered;
    }

    public void Dispose()
    {
        _db.Dispose();
        _mutex.Dispose();
    }
}
