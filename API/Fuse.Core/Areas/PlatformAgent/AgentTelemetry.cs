namespace Fuse.Core.Areas.PlatformAgent;

public sealed record AgentDiskReading(string Id, string Path, long? TotalBytes, long? AvailableBytes);
public sealed record AgentGpuReading(string Id, string Name, double? UtilizationPercent);

public sealed record AgentTelemetry(double? CpuAveragePercent, double? CpuPeakPercent,
    long? MemoryTotalBytes, long? MemoryAvailableBytes, AgentDiskReading[] Disks, AgentGpuReading[] Gpus,
    DateTime? SampledAt = null);

public sealed record AgentAssessment(DateTime CheckedAt, DateTime? IndexUpdatedAt,
    string[] Pending, string[] Held, int? PendingCount, int? HeldCount, bool? RebootRequired, string? Error);

public sealed record AgentCheckJob(Guid Id, string State, DateTime RequestedAt, DateTime? CompletedAt = null);

public sealed record AgentCheckIn(int ProtocolVersion = 1, AgentTelemetry? Telemetry = null,
    AgentAssessment? Assessment = null, Guid? CompletedJobId = null);

public sealed record AgentJobDelivery(Guid Id, string Type);
public sealed record AgentCheckInResult(AgentJobDelivery? Job = null);
public sealed record AgentHistoryPoint(DateTime At, double? CpuAveragePercent, double? CpuPeakPercent,
    double? MemoryUsedPercent, double? DiskUsedPercent, double? GpuUsagePercent);
public sealed record AgentHistory(DateTime From, DateTime To, int IntervalSeconds, IReadOnlyList<AgentHistoryPoint> Points);
