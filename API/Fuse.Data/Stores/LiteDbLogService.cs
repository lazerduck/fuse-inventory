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

    /// <summary>
    /// Build a server-side LiteDB query with all filters that can be translated to BsonExpression.
    /// SearchText is applied in-memory because case-insensitive substring matching
    /// cannot be translated by LiteDB's query engine.
    /// </summary>
    private static ILiteQueryable<SystemLogEntry> BuildServerQuery(ILiteCollection<SystemLogEntry> logs, SystemLogQuery query)
    {
        var q = logs.Query();

        if (query.MinLevel.HasValue)
            q = q.Where(x => x.Level >= query.MinLevel.Value);

        if (!string.IsNullOrWhiteSpace(query.Area))
            q = q.Where(x => x.Area == query.Area);

        if (query.StartTime.HasValue)
            q = q.Where(x => x.Timestamp >= query.StartTime.Value);

        if (query.EndTime.HasValue)
            q = q.Where(x => x.Timestamp <= query.EndTime.Value);

        return q;
    }

    private static bool MatchesSearchText(SystemLogEntry entry, string searchText) =>
        !string.IsNullOrWhiteSpace(searchText) &&
        (
            !string.IsNullOrWhiteSpace(entry.Message) && entry.Message.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(entry.Details) && entry.Details.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(entry.Exception) && entry.Exception.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(entry.Area) && entry.Area.Contains(searchText, StringComparison.OrdinalIgnoreCase)
        );

    public async Task<SystemLogResult> QueryAsync(SystemLogQuery query, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 50 : query.PageSize;
        var skip = (page - 1) * pageSize;

        await _mutex.WaitAsync(ct);
        try
        {
            var result = await Task.Run(() =>
            {
                // Server-side: apply indexed filters (MinLevel, Area, StartTime, EndTime)
                var serverQuery = BuildServerQuery(_logs, query);

                // In-memory: apply case-insensitive SearchText filter
                // (LiteDB cannot translate case-insensitive substring matching)
                var filtered = query.SearchText != null && !string.IsNullOrWhiteSpace(query.SearchText.Trim())
                    ? serverQuery.ToList().Where(e => MatchesSearchText(e, query.SearchText!.Trim()))
                    : serverQuery.ToList();

                // Count after all filters applied
                var totalCount = filtered.Count();

                // Sort and paginate in-memory (timestamp ordering + pagination)
                var pageLogs = filtered
                    .OrderByDescending(x => x.Timestamp)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList();

                return new SystemLogResult(pageLogs, totalCount, page, pageSize);
            }, ct);

            return result;
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
            var counts = await Task.Run(() =>
            {
                // Server-side: apply indexed filters
                var serverQuery = BuildServerQuery(_logs, query);

                // In-memory: apply SearchText filter
                IEnumerable<SystemLogEntry> filtered;
                if (query.SearchText != null && !string.IsNullOrWhiteSpace(query.SearchText.Trim()))
                {
                    var searchText = query.SearchText.Trim();
                    filtered = serverQuery.ToList().Where(e => MatchesSearchText(e, searchText));
                }
                else
                {
                    filtered = serverQuery.ToList();
                }

                return new SystemLogCounts
                {
                    Debug = filtered.Count(x => x.Level == LogLevel.Debug),
                    Info = filtered.Count(x => x.Level == LogLevel.Info),
                    Warning = filtered.Count(x => x.Level == LogLevel.Warning),
                    Error = filtered.Count(x => x.Level == LogLevel.Error)
                };
            }, ct);

            return counts;
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
            // Server-side: project Area column via Query() (pushes to LiteDB)
            // Distinct/ordering done in-memory since LiteDB queryable doesn't support Distinct
            var areas = await Task.Run(() =>
            {
                var queryableAreas = _logs.Query()
                    .Select(x => x.Area)
                    .ToList();

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var result = new List<string>();
                foreach (var area in queryableAreas)
                {
                    if (!string.IsNullOrWhiteSpace(area) && seen.Add(area))
                        result.Add(area);
                }
                result.Sort(StringComparer.OrdinalIgnoreCase);
                return (IReadOnlyList<string>)result;
            }, ct);

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
            // Server-side count (already was, no change needed)
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
            // Server-side delete (already was, no change needed)
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

    public void Dispose()
    {
        _db.Dispose();
        _mutex.Dispose();
    }
}