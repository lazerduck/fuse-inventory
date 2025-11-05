using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IFuseStore
{
    Task<Snapshot> GetAsync(CancellationToken ct = default); // fast, returns cached or loads once
    Task<Snapshot> LoadAsync(CancellationToken ct = default); // forces (re)load from disk
    Task SaveAsync(Snapshot snapshot, CancellationToken ct = default);
    Task UpdateAsync(Func<Snapshot, Snapshot> mutate, CancellationToken ct = default);
    Snapshot? Current { get; }               // null until first load
    event Action<Snapshot>? Changed;         // fire after successful save
}