using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IFuseStore
{
    Task<Snapshot> GetAsync(CancellationToken ct = default); // fast, returns cached or loads once
    Task<T> GetAsync<T>(Func<Snapshot, T> selector, CancellationToken ct = default);
    Task<Snapshot> LoadAsync(CancellationToken ct = default); // forces (re)load from disk
    // Replaces the entire snapshot; use UpdateAsync for changes based on current state.
    Task SaveAsync(Snapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Serializes reading, mutating, and persisting the current snapshot.
    /// Derive changes from the callback's snapshot, without mutating it in place.
    /// The callback must not call back into store operations that acquire the write lock.
    /// </summary>
    Task UpdateAsync(Func<Snapshot, Snapshot> mutate, CancellationToken ct = default);
    Snapshot? Current { get; }               // null until first load
    event Action<Snapshot>? Changed;         // fire after successful save
}

/// <summary>
/// Implemented by persistent stores that can create a recovery point before a bulk update.
/// </summary>
public interface IBackupCapableFuseStore
{
    Task CreateBackupAsync(CancellationToken ct = default);
}
