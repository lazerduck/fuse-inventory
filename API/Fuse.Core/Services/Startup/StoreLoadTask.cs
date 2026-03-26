using Fuse.Core.Interfaces;

namespace Fuse.Core.Services.Startup;

/// <summary>
/// Ensures the data store is loaded from disk before any other startup work runs.
/// </summary>
public class StoreLoadTask : IStartupTask
{
    private readonly IFuseStore _store;

    public StoreLoadTask(IFuseStore store)
    {
        _store = store;
    }

    public int Order => 1;

    public Task RunAsync(CancellationToken ct = default)
        => _store.LoadAsync(ct);
}
