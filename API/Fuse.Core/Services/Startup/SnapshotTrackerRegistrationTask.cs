using Fuse.Core.Interfaces;
using Fuse.Core.Areas.Activity;

namespace Fuse.Core.Services.Startup;

/// <summary>
/// Subscribes <see cref="SnapshotChangeTracker"/> to <see cref="IFuseStore.Changed"/>
/// so that entity version history is recorded automatically on every store mutation.
/// Must run after the store has been loaded (<see cref="StoreLoadTask"/>).
/// </summary>
public class SnapshotTrackerRegistrationTask : IStartupTask
{
    private readonly IFuseStore _store;
    private readonly IVersionHistoryService _versionHistoryService;

    public SnapshotTrackerRegistrationTask(IFuseStore store, IVersionHistoryService versionHistoryService)
    {
        _store = store;
        _versionHistoryService = versionHistoryService;
    }

    public int Order => 4;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var initialSnapshot = await _store.GetAsync(ct);
        SnapshotChangeTracker.RegisterWithStore(_store, _versionHistoryService, initialSnapshot);
    }
}
