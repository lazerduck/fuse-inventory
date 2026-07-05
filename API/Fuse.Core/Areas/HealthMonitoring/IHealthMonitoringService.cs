using Fuse.Core.Models;

namespace Fuse.Core.Areas.HealthMonitoring;

public interface IHealthMonitoringService
{
    Task<HealthOverview> GetOverviewAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InstanceHealthTransition>> GetHistoryAsync(Guid instanceId, CancellationToken ct = default);
}
