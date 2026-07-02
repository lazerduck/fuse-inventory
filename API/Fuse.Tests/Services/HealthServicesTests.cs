using Fuse.Core.Areas.Application;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Services;

public class HealthServicesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"fuse-health-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public async Task HealthCheck_CreatesMissingDirectoryAndReportsDegradedOptionalFiles()
    {
        var status = await new HealthCheckService(_directory).GetStatusAsync();

        Assert.False(status.IsHealthy);
        Assert.Equal("Unhealthy", status.Status);
        Assert.Equal(HealthStatusType.Healthy, status.Components["data-directory"].Type);
        Assert.Equal(HealthStatusType.Degraded, status.Components["json-files"].Type);
        Assert.Equal(HealthStatusType.Degraded, status.Components["lite-db"].Type);
        Assert.False(await new HealthCheckService(_directory).IsReadyAsync());
    }

    [Fact]
    public async Task HealthCheck_ReportsHealthyForValidJsonAndNonEmptyDatabase()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "data.json"), "{\"ok\":true}");
        await File.WriteAllTextAsync(Path.Combine(_directory, "empty.json"), " ");
        await File.WriteAllTextAsync(Path.Combine(_directory, "audit.db"), "content");
        var service = new HealthCheckService(_directory);

        var status = await service.GetStatusAsync();

        Assert.True(status.IsHealthy);
        Assert.All(status.Components.Values, component => Assert.Equal(HealthStatusType.Healthy, component.Type));
        Assert.True(await service.IsReadyAsync());
    }

    [Fact]
    public async Task HealthCheck_ReportsCorruptJsonAndEmptyDatabase()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "bad.json"), "not json");
        File.Create(Path.Combine(_directory, "audit.db")).Dispose();

        var status = await new HealthCheckService(_directory).GetStatusAsync();

        Assert.Equal(HealthStatusType.Unhealthy, status.Components["json-files"].Type);
        Assert.Contains("bad.json", status.Components["json-files"].Description);
        Assert.Equal(HealthStatusType.Unhealthy, status.Components["lite-db"].Type);
    }

    [Fact]
    public async Task ApplicationHealth_ThrowsForMissingApplication()
    {
        var service = new ApplicationHealthService(new InMemoryFuseStore());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetApplicationHealth(Guid.NewGuid()));
    }

    [Fact]
    public async Task ApplicationHealth_MapsCompletenessAndCounts()
    {
        var instance = new ApplicationInstance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new Uri("https://app"),
            new Uri("https://app/health"), new Uri("https://app/openapi"), "1.0", [], [Guid.NewGuid()], DateTime.UtcNow, DateTime.UtcNow);
        var complete = new Application(Guid.NewGuid(), "Complete", "1.0", "desc", "owner", null, "net", null, null,
            [Guid.NewGuid()], [instance], [new ApplicationPipeline(Guid.NewGuid(), "main", null)], DateTime.UtcNow, DateTime.UtcNow);
        var incomplete = new Application(Guid.NewGuid(), "Incomplete", null, null, null, null, null, null, null,
            [], [instance with { Id = Guid.NewGuid(), PlatformId = null, BaseUri = null, HealthUri = null, OpenApiUri = null, Version = null, TagIds = [] }], [], DateTime.UtcNow, DateTime.UtcNow);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Applications = [complete, incomplete] });
        var service = new ApplicationHealthService(store);

        var health = await service.GetApplicationHealth(complete.Id);
        var all = await service.GetAllApplicationHealths();

        Assert.True(health.VersionSet && health.DescriptionSet && health.OwnerSet && health.FrameworkSet);
        Assert.Equal(1, health.TagCount);
        Assert.Equal(1, health.PipelineCount);
        Assert.True(Assert.Single(health.InstanceHealths).PlatformSet);
        Assert.Equal(2, all.Count);
        var missing = all.Single(x => x.ApplicationId == incomplete.Id);
        Assert.False(missing.VersionSet || missing.DescriptionSet || missing.OwnerSet || missing.FrameworkSet);
        Assert.False(Assert.Single(missing.InstanceHealths).PlatformSet);
    }
}
