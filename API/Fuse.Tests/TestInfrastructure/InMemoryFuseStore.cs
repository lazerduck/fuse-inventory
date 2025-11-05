using System.Collections.Concurrent;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Tests.TestInfrastructure;

public sealed class InMemoryFuseStore : IFuseStore
{
    private readonly object _lock = new();
    private Snapshot _snapshot;

    public InMemoryFuseStore(Snapshot? initial = null)
    {
        _snapshot = initial ?? new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Servers: Array.Empty<Server>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>()
        );
    }

    public Snapshot? Current => _snapshot;

    public event Action<Snapshot>? Changed;

    public Task<Snapshot> GetAsync(CancellationToken ct = default)
        => Task.FromResult(_snapshot);

    public Task<Snapshot> LoadAsync(CancellationToken ct = default)
        => Task.FromResult(_snapshot);

    public Task SaveAsync(Snapshot snapshot, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _snapshot = snapshot;
        }
        Changed?.Invoke(_snapshot);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Func<Snapshot, Snapshot> mutate, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _snapshot = mutate(_snapshot);
        }
        Changed?.Invoke(_snapshot);
        return Task.CompletedTask;
    }
}
