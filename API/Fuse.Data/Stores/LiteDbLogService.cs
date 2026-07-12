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
            var logs = await Task.Run(() => _logs.FindAll().ToList(), ct);
            var filtered = ApplyQuery(logs, query);
            var totalCount = filtered.Count;
            var pageLogs = filtered
                .OrderByDescending(x => x.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

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
            var logs = await Task.Run(() => _logs.FindAll().ToList(), ct);
            var filtered = ApplyQuery(logs, query);

            return new SystemLogCounts
            {
                Debug = filtered.Count(x => x.Level == LogLevel.Debug),
                Info = filtered.Count(x => x.Level == LogLevel.Info),
                Warning = filtered.Count(x => x.Level == LogLevel.Warning),
                Error = filtered.Count(x => x.Level == LogLevel.Error)
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
            var areas = await Task.Run(() => _logs.FindAll()
                .Select(x => x.Area)
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

    private static List<SystemLogEntry> ApplyQuery(IEnumerable<SystemLogEntry> logs, SystemLogQuery query)
    {
        var filtered = logs;

        if (query.MinLevel.HasValue)
            filtered = filtered.Where(x => x.Level >= query.MinLevel.Value);

        if (!string.IsNullOrWhiteSpace(query.Area))
            filtered = filtered.Where(x => string.Equals(x.Area, query.Area, StringComparison.OrdinalIgnoreCase));

        if (query.StartTime.HasValue)
            filtered = filtered.Where(x => x.Timestamp >= query.StartTime.Value);

        if (query.EndTime.HasValue)
            filtered = filtered.Where(x => x.Timestamp <= query.EndTime.Value);

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText.Trim();
            filtered = filtered.Where(x =>
                ContainsIgnoreCase(x.Message, searchText)
                || ContainsIgnoreCase(x.Details, searchText)
                || ContainsIgnoreCase(x.Exception, searchText)
                || ContainsIgnoreCase(x.Area, searchText));
        }

        return filtered.ToList();
    }

    private static bool ContainsIgnoreCase(string? value, string searchText) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        _db.Dispose();
        _mutex.Dispose();
    }
}
