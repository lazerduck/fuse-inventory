using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.PlatformAgent;

public sealed record AgentTarget(Guid PlatformId, Guid? NodeId)
{
    public bool ExistsIn(Snapshot snapshot)
    {
        var platform = snapshot.Platforms.FirstOrDefault(p => p.Id == PlatformId);
        if (NodeId is { } nodeId)
            return platform?.Kind == PlatformKind.Cluster && platform.Nodes?.Any(n => n.Id == nodeId) == true;
        return platform?.Kind is PlatformKind.Server or PlatformKind.ContainerHost;
    }
}

public sealed record AgentReportingStatus(string State, DateTime? LastSeenAt = null,
    string? Hostname = null, string? Version = null, DateTime? FirstSeenAt = null, DateTime? ExpiresAt = null,
    AgentTelemetry? Telemetry = null, DateTime? TelemetryAt = null, AgentAssessment? Assessment = null,
    AgentCheckJob? CheckJob = null, bool SupportsAssessment = false);

public sealed record AgentFleetDevice(Guid PlatformId, string PlatformName, PlatformKind PlatformKind,
    Guid? NodeId, string? NodeName, string? Hostname, string State, DateTime LastSeenAt, DateTime? TelemetryAt,
    double? CpuAveragePercent, long? MemoryTotalBytes, long? MemoryAvailableBytes, decimal? DiskTotalBytes,
    decimal? DiskAvailableBytes, DateTime? AssessmentAt, DateTime? IndexUpdatedAt, int? PendingCount,
    string? AssessmentError);

public sealed record AgentInvitation(string Ticket, DateTime ExpiresAt);

public interface IAgentReportingStore
{
    Task<IReadOnlyList<AgentFleetDevice>> FleetAsync(CancellationToken ct);
    Task<AgentReportingStatus?> StatusAsync(AgentTarget target, CancellationToken ct);
    Task<Result<AgentInvitation>> InviteAsync(AgentTarget target, bool replace, CancellationToken ct);
    Task<bool> RevokeAsync(AgentTarget target, CancellationToken ct);
    Task<bool> PairAsync(string ticket, string credential, CancellationToken ct);
    Task<AgentCheckInResult?> ReportAsync(string credential, string hostname, string version, AgentCheckIn report, CancellationToken ct);
    Task<bool> IsActiveCredentialAsync(string credential, CancellationToken ct);
    Task<Result<AgentCheckJob>> RequestCheckAsync(AgentTarget target, CancellationToken ct);
    Task<AgentHistory?> HistoryAsync(AgentTarget target, int hours, CancellationToken ct);
    void PruneHistory();
}
