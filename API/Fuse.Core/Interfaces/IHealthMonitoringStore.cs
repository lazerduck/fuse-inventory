using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IHealthMonitoringStore
{
    Task<IReadOnlyList<InstanceHealthResult>> GetCurrentAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InstanceHealthTransition>> GetHistoryAsync(Guid instanceId, DateTime since, CancellationToken ct = default);
    Task SaveAsync(InstanceHealthResult result, CancellationToken ct = default);
    Task ClearCurrentAsync(CancellationToken ct = default);
    Task RemoveOrphansAsync(IReadOnlySet<Guid> instanceIds, CancellationToken ct = default);
    Task DeleteTransitionsOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}
