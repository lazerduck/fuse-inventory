using Fuse.Core.Responses;

namespace Fuse.Core.Interfaces;

public interface IKumaHealthService
{
    /// <summary>
    /// Gets the cached health status for a given monitor URL.
    /// </summary>
    HealthStatusResponse? GetHealthStatus(string monitorUrl);
}
