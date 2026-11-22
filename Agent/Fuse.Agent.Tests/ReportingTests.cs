using System.Runtime.Versioning;
using System.Text.Json;
using Xunit;

[assembly: SupportedOSPlatform("linux")]

namespace Fuse.Agent.Tests;

public sealed class ReportingTests
{
    [Fact]
    public void CpuUsesIntervalDeltasAndPreservesPeaks()
    {
        var sampler = new HostTelemetry();
        sampler.SampleCpuRow("cpu 100 0 0 0 0 0 0 0 70 0", TimeSpan.Zero);
        Assert.Null(sampler.TakeCpu().Average);
        sampler.SampleCpuRow("cpu 200 0 0 0 0 0 0 0 100 0", TimeSpan.FromSeconds(5));
        sampler.SampleCpuRow("cpu 200 0 0 200 0 0 0 0 100 0", TimeSpan.FromSeconds(10));
        var result = sampler.TakeCpu();
        Assert.Equal(100d / 3, result.Average!.Value, 5);
        Assert.Equal(100, result.Peak);
        sampler.SampleCpuRow("cpu 1 0 1 1 0 0 0 0 0 0", TimeSpan.FromSeconds(15));
        Assert.Null(sampler.TakeCpu().Average);
    }

    [Fact]
    public void AptSimulationSeparatesRoutineCandidatesFromHeldPackages()
    {
        var packages = new PackageList();
        foreach (var line in new[]
        {
            "The following packages have been kept back:", "  alpha:arm64", "The following NEW packages will be installed:",
            "  beta", "Inst beta (2.0 Debian [arm64])", "Inst gamma [1.0] (2.0 Debian [arm64])",
            "The following upgrades have been deferred due to phasing:", "  delta", "Conf beta (2.0 Debian [arm64])"
        }) packages.ReadSimulationLine(line);
        packages.AddHold("alpha:arm64");
        Assert.Equal(new[] { "beta", "gamma" }, packages.Pending.Order());
        Assert.Equal(new[] { "alpha:arm64", "delta" }, packages.Held.Order());
    }

    [Fact]
    public void LongMultibyteMountsCannotPreventHeartbeatOrAssessmentAcknowledgement()
    {
        var list = Enumerable.Range(0, 64).Select(i =>
        {
            var path = "/" + i + new string('盤', 245);
            return new DiskReading(path, path, 900, 700);
        }).ToList();
        var id = Guid.NewGuid();
        var assessment = new AptAssessment(DateTimeOffset.UtcNow, null, ["beta"], [], 1, 0, null, null);
        var reading = new TelemetryReading(DateTimeOffset.UtcNow, 12, 60, 100, 80, list, []);
        var bytes = new WorkerCheckIn("test", "node", "0.0.2", reading, assessment, id).Encode();
        Assert.True(bytes.Length < 96 * 1024);
        using var doc = JsonDocument.Parse(bytes);
        Assert.Equal(id, doc.RootElement.GetProperty("completedJobId").GetGuid());
        Assert.Equal(1, doc.RootElement.GetProperty("assessment").GetProperty("pendingCount").GetInt32());
        var disks = doc.RootElement.GetProperty("telemetry").GetProperty("disks").GetArrayLength();
        Assert.InRange(disks, 1, 63);
    }

    [Fact]
    public void ReplayedReadOnlyJobAfterRestartReturnsSavedResult()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var id = Guid.NewGuid();
            var assessment = new AptAssessment(DateTimeOffset.UtcNow.AddMinutes(-5), null, [], [], 0, 0, null, null);
            File.WriteAllText(Path.Combine(dir.FullName, "apt-job.json"),
                JsonSerializer.Serialize(new AssessmentSchedule.Completion(id, assessment), AgentConnection.Json));
            var checks = new AssessmentSchedule(dir.FullName);
            checks.Requested(id);
            Assert.Equal(id, checks.CompletedJobId);
            Assert.Equal(assessment.CheckedAt, checks.Report!.CheckedAt);
            checks.Acknowledged();
            Assert.Null(checks.Report);
            Assert.Null(checks.CompletedJobId);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public async Task MissingIndexesLeaveBothPackageCountsUnknown()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var result = await AptInspection.InspectAsync(dir.FullName, default);
            Assert.NotNull(result.Error);
            Assert.Null(result.HeldCount);
            Assert.Null(result.PendingCount);
            Assert.Empty(result.Pending);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public async Task RequestedCheckStartsAFreshAssessmentAfterAnInflightScheduledRead()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            List<TaskCompletionSource<AptAssessment>> reads = [];
            var checks = new AssessmentSchedule(dir.FullName, _ =>
            {
                var read = new TaskCompletionSource<AptAssessment>();
                reads.Add(read);
                return read.Task;
            });
            var id = Guid.NewGuid();
            var before = new AptAssessment(DateTimeOffset.UtcNow, null, [], [], 0, 0, null, null);
            var after = before with { CheckedAt = before.CheckedAt.AddSeconds(1) };
            await checks.TickAsync(default);
            checks.Requested(id);
            reads[0].SetResult(before);
            await checks.TickAsync(default);
            Assert.Null(checks.CompletedJobId);
            Assert.Equal(before, checks.Report);
            checks.Acknowledged();
            await checks.TickAsync(default);
            checks.Requested(id);
            reads[1].SetResult(after);
            await checks.TickAsync(default);
            Assert.Equal(after, checks.Report);
            Assert.Equal(id, checks.CompletedJobId);
            checks.Acknowledged();
            await checks.TickAsync(default);
            Assert.Equal(2, reads.Count);
        }
        finally { dir.Delete(true); }
    }
}
