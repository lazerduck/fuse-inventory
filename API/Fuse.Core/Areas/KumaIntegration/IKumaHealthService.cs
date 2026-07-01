using Fuse.Core.Responses;

namespace Fuse.Core.Areas.KumaIntegration;

public interface IKumaHealthService
{
    /// <summary>
    /// Gets the cached health status for a given monitor URL.
    /// </summary>
    HealthStatusResponse? GetHealthStatus(string monitorUrl);
}
