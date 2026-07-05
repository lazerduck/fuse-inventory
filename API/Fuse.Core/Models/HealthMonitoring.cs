namespace Fuse.Core.Models;

public enum InstanceHealthState { Unknown, Healthy, Unhealthy }

public sealed record InstanceHealthResult(
    Guid InstanceId,
    Guid ApplicationId,
    string ApplicationName,
    Guid EnvironmentId,
    string? EnvironmentName,
    string HealthUrl,
    HealthCheckProvider Provider,
    InstanceHealthState State,
    DateTime CheckedAt,
    long? DurationMs = null,
    int? HttpStatusCode = null,
    string? FailureCategory = null,
    string? ResponseSummary = null,
    string? MonitorName = null,
    bool ResponseTruncated = false,
    bool ResponseRedacted = false);

public sealed record InstanceHealthTransition(
    Guid Id,
    Guid InstanceId,
    Guid ApplicationId,
    HealthCheckProvider Provider,
    InstanceHealthState PreviousState,
    InstanceHealthState State,
    DateTime CheckedAt,
    long? DurationMs = null,
    int? HttpStatusCode = null,
    string? FailureCategory = null,
    string? ResponseSummary = null,
    string? MonitorName = null,
    bool ResponseTruncated = false,
    bool ResponseRedacted = false);

public sealed record HealthOverview(
    HealthCheckProvider Provider,
    bool ProviderAvailable,
    string? UnavailableReason,
    int Healthy,
    int Unhealthy,
    int Unknown,
    IReadOnlyList<InstanceHealthResult> Results);
