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
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Identities: Array.Empty<Identity>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            SecretProviders: Array.Empty<SecretProvider>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>()),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SqlIntegrations: Array.Empty<SqlIntegration>()
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
