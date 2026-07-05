using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using LiteDB;

namespace Fuse.Data.Stores;

public sealed class LiteDbHealthMonitoringStore : IHealthMonitoringStore, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<InstanceHealthResult> _current;
    private readonly ILiteCollection<InstanceHealthTransition> _history;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public LiteDbHealthMonitoringStore(string dataDirectory)
    {
        _db = new LiteDatabase(Path.Combine(dataDirectory, "healthchecks.db"));
        _current = _db.GetCollection<InstanceHealthResult>("current");
        _history = _db.GetCollection<InstanceHealthTransition>("transitions");
        _current.EnsureIndex(x => x.InstanceId, true);
        _history.EnsureIndex(x => x.InstanceId);
        _history.EnsureIndex(x => x.CheckedAt);
    }

    public Task<IReadOnlyList<InstanceHealthResult>> GetCurrentAsync(CancellationToken ct = default) => LockedAsync<IReadOnlyList<InstanceHealthResult>>(() => _current.FindAll().ToList(), ct);
    public Task<IReadOnlyList<InstanceHealthTransition>> GetHistoryAsync(Guid instanceId, DateTime since, CancellationToken ct = default) =>
        LockedAsync<IReadOnlyList<InstanceHealthTransition>>(() => _history.Query()
            .Where(x => x.InstanceId == instanceId && x.CheckedAt >= since)
            .OrderByDescending(x => x.CheckedAt)
            .ToList()
            .Where(IsActualTransition)
            .ToList(), ct);

    public Task SaveAsync(InstanceHealthResult result, CancellationToken ct = default) => LockedAsync(() =>
    {
        var previous = _current.FindOne(x => x.InstanceId == result.InstanceId);
        var previousStableState = previous is null
            ? null
            : IsStable(previous.State)
                ? previous.State
                : _history.Query()
                    .Where(x => x.InstanceId == result.InstanceId)
                    .OrderByDescending(x => x.CheckedAt)
                    .ToList()
                    .Select(x => (InstanceHealthState?)x.State)
                    .FirstOrDefault(x => x.HasValue && IsStable(x.Value));

        if (previous is null && IsStable(result.State))
            _history.Insert(new InstanceHealthTransition(Guid.NewGuid(), result.InstanceId, result.ApplicationId, result.Provider,
                InstanceHealthState.Unknown, result.State, result.CheckedAt, result.DurationMs, result.HttpStatusCode,
                result.FailureCategory, result.ResponseSummary, result.MonitorName, result.ResponseTruncated, result.ResponseRedacted));
        else if (previousStableState.HasValue && IsActualTransition(previousStableState.Value, result.State))
            _history.Insert(new InstanceHealthTransition(Guid.NewGuid(), result.InstanceId, result.ApplicationId, result.Provider,
                previousStableState.Value, result.State, result.CheckedAt, result.DurationMs,
                result.HttpStatusCode, result.FailureCategory, result.ResponseSummary, result.MonitorName,
                result.ResponseTruncated, result.ResponseRedacted));
        _current.DeleteMany(x => x.InstanceId == result.InstanceId);
        _current.Insert(result);
    }, ct);

    public Task ClearCurrentAsync(CancellationToken ct = default) => LockedAsync(() => _current.DeleteAll(), ct);
    public Task RemoveOrphansAsync(IReadOnlySet<Guid> ids, CancellationToken ct = default) => LockedAsync(() =>
    {
        foreach (var item in _current.FindAll().Where(x => !ids.Contains(x.InstanceId)).ToList())
            _current.DeleteMany(x => x.InstanceId == item.InstanceId);
        foreach (var item in _history.FindAll().Where(x => !ids.Contains(x.InstanceId)).ToList())
            _history.Delete(item.Id);
    }, ct);
    public Task DeleteTransitionsOlderThanAsync(DateTime cutoff, CancellationToken ct = default) => LockedAsync(() => _history.DeleteMany(x => x.CheckedAt < cutoff), ct);

    private static bool IsActualTransition(InstanceHealthTransition transition) =>
        IsActualTransition(transition.PreviousState, transition.State);

    private static bool IsActualTransition(InstanceHealthState previous, InstanceHealthState current) =>
        previous != current &&
        IsStable(previous) && IsStable(current);

    private static bool IsStable(InstanceHealthState state) =>
        state is InstanceHealthState.Healthy or InstanceHealthState.Unhealthy;

    private async Task<T> LockedAsync<T>(Func<T> action, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try { return await Task.Run(action, ct); }
        finally { _mutex.Release(); }
    }
    private async Task LockedAsync(Action action, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try { await Task.Run(action, ct); }
        finally { _mutex.Release(); }
    }
    public void Dispose() { _db.Dispose(); _mutex.Dispose(); }
}
