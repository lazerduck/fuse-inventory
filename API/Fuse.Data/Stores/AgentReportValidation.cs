using Fuse.Core.Areas.PlatformAgent;

namespace Fuse.Data.Stores;

internal static class AgentReportValidation
{
    public static bool Valid(AgentAssessment a) => a.CheckedAt.Kind != DateTimeKind.Unspecified &&
        a.IndexUpdatedAt is not { Kind: DateTimeKind.Unspecified } &&
        Packages(a.Pending, a.PendingCount) && Packages(a.Held, a.HeldCount) &&
        (a.Error is null || Text(a.Error, 1024));

    public static bool Valid(AgentTelemetry m) => m.SampledAt is not { Kind: DateTimeKind.Unspecified } &&
            Percent(m.CpuAveragePercent) && Percent(m.CpuPeakPercent) &&
            Capacity(m.MemoryTotalBytes, m.MemoryAvailableBytes) &&
            m.Disks is { Length: <= 64 } && m.Disks.All(d =>
                d is not null && Text(d.Id, 255) && Text(d.Path, 1024) && Capacity(d.TotalBytes, d.AvailableBytes)) &&
            m.Gpus is { Length: <= 32 } && m.Gpus.All(g =>
                g is not null && Text(g.Id, 255) && Text(g.Name, 255) && Percent(g.UtilizationPercent));

    private static bool Capacity(long? total, long? available) =>
        total is not < 0 && available is not < 0 && !(available > total);
    private static bool Percent(double? value) => value is null or (>= 0 and <= 100);
    private static bool Packages(string[]? names, int? count) => names is { Length: <= 100 } &&
        !(count < names.Length) && names.All(name => Text(name, 255));
    private static bool Text(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength && !value.Any(char.IsControl);
}
