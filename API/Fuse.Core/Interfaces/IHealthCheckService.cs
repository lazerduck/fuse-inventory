namespace Fuse.Core.Interfaces;

/// <summary>
/// Checks the health of the application (data store, file system, etc.)
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Returns true if the application is fundamentally operational.
    /// This is the "ready" check — used by orchestrators to determine
    /// whether the service can accept requests.
    /// </summary>
    Task<bool> IsReadyAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns detailed health status including component checks.
    /// </summary>
    Task<HealthStatus> GetStatusAsync(CancellationToken ct = default);
}

/// <summary>
/// Overall health status.
/// </summary>
public record HealthStatus(
    bool IsHealthy,
    string Status,
    IReadOnlyDictionary<string, ComponentHealth> Components
);

/// <summary>
/// Health status for an individual component.
/// </summary>
public record ComponentHealth(
    string Name,
    HealthStatusType Type,
    string? Description
);

/// <summary>
/// Health status type for a component.
/// </summary>
public enum HealthStatusType
{
    Healthy,
    Unhealthy,
    Degraded
}