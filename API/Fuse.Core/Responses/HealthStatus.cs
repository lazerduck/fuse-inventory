namespace Fuse.Core.Responses;

public record HealthStatusResponse
(
    string MonitorUrl,
    MonitorStatus Status,
    string? MonitorName,
    DateTime LastChecked
);

public enum MonitorStatus
{
    Up = 1,
    Down = 0,
    Pending = 2,
    Maintenance = 3
}
