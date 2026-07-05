using Fuse.Core.Models;
using Fuse.Data.Stores;
using Xunit;

namespace Fuse.Tests.Services;

public sealed class HealthMonitoringStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"fuse-health-{Guid.NewGuid():N}");
    private readonly LiteDbHealthMonitoringStore _store;

    public HealthMonitoringStoreTests()
    {
        Directory.CreateDirectory(_directory);
        _store = new LiteDbHealthMonitoringStore(_directory);
    }

    [Fact]
    public async Task Save_UpdatesCurrentButOnlyRecordsStateTransitions()
    {
        var first = Result(InstanceHealthState.Healthy, DateTime.UtcNow);
        await _store.SaveAsync(first);
        await _store.SaveAsync(first with { CheckedAt = first.CheckedAt.AddMinutes(1), DurationMs = 25 });
        await _store.SaveAsync(first with { CheckedAt = first.CheckedAt.AddMinutes(2), State = InstanceHealthState.Unhealthy });

        var current = Assert.Single(await _store.GetCurrentAsync());
        Assert.Equal(InstanceHealthState.Unhealthy, current.State);
        Assert.Single(await _store.GetHistoryAsync(first.InstanceId, DateTime.MinValue));
    }

    [Fact]
    public async Task Save_DoesNotRecordBaselineOrUrlChangesWithoutAStateChange()
    {
        var first = Result(InstanceHealthState.Healthy, DateTime.UtcNow);
        await _store.SaveAsync(first);
        await _store.SaveAsync(first with { HealthUrl = "https://example.test/ready", CheckedAt = first.CheckedAt.AddMinutes(1) });
        Assert.Empty(await _store.GetHistoryAsync(first.InstanceId, DateTime.MinValue));
    }

    [Fact]
    public async Task Save_TracksStableTransitionAcrossAnUnknownResult()
    {
        var first = Result(InstanceHealthState.Healthy, DateTime.UtcNow);
        await _store.SaveAsync(first);
        await _store.SaveAsync(first with { State = InstanceHealthState.Unknown, CheckedAt = first.CheckedAt.AddMinutes(1) });
        await _store.SaveAsync(first with { State = InstanceHealthState.Unhealthy, CheckedAt = first.CheckedAt.AddMinutes(2) });

        var transition = Assert.Single(await _store.GetHistoryAsync(first.InstanceId, DateTime.MinValue));
        Assert.Equal(InstanceHealthState.Healthy, transition.PreviousState);
        Assert.Equal(InstanceHealthState.Unhealthy, transition.State);
    }

    [Fact]
    public async Task Cleanup_RemovesExpiredTransitionsButKeepsCurrent()
    {
        var result = Result(InstanceHealthState.Healthy, DateTime.UtcNow.AddDays(-8));
        await _store.SaveAsync(result);
        await _store.DeleteTransitionsOlderThanAsync(DateTime.UtcNow.AddDays(-7));
        Assert.Empty(await _store.GetHistoryAsync(result.InstanceId, DateTime.MinValue));
        Assert.Single(await _store.GetCurrentAsync());
    }

    private static InstanceHealthResult Result(InstanceHealthState state, DateTime at) => new(
        Guid.NewGuid(), Guid.NewGuid(), "App", Guid.NewGuid(), "Production", "https://example.test/health",
        HealthCheckProvider.Internal, state, at);

    public void Dispose()
    {
        _store.Dispose();
        Directory.Delete(_directory, true);
    }
}
