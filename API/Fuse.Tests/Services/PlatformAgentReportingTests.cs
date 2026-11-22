using Fuse.API.Agents;
using Fuse.Core.Areas.PlatformAgent;
using Fuse.Core.Configs;
using Fuse.Core.Models;
using Fuse.Data.Stores;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Services;

public sealed class PlatformAgentReportingTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("fuse-agent-").FullName;
    private readonly Clock _clock = new();
    private readonly InMemoryFuseStore _inventory = new();
    private readonly Guid _platformId = Guid.NewGuid();
    private readonly Guid _nodeId = Guid.NewGuid();
    private const string Credential = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherCredential = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private AgentTarget Target => new(_platformId, _nodeId);
    private LiteDbAgentReportingStore Open() => new(_inventory, _directory, _clock);

    private Task AddCluster() => _inventory.UpdateAsync(s => s with
    {
        Platforms = [new Platform(_platformId, "cluster", null, null, PlatformKind.Cluster, [], null, [],
            DateTime.UtcNow, DateTime.UtcNow, [new PlatformNode(_nodeId, "node", null, null, [], null)])]
    });

    [Fact]
    public async Task OnlyInstalledCheckInActivatesNodeAndSurvivesRestart()
    {
        await AddCluster();
        using (var db = Open())
        {
            Assert.Null(await db.StatusAsync(new(_platformId, null), default));
            var invitation = (await db.InviteAsync(Target, false, default)).Value!;
            Assert.True(await db.PairAsync(invitation.Ticket, Credential, default));
            Assert.True(await db.PairAsync(invitation.Ticket, Credential, default)); // lost acknowledgement
            Assert.False(await db.PairAsync(invitation.Ticket, OtherCredential, default));
            var pending = await db.StatusAsync(Target, default);
            Assert.Equal("pending", pending!.State);
            Assert.Null(pending.FirstSeenAt);
            Assert.NotNull(await db.ReportAsync(Credential, "server-01", "1.0", new(), default));
            Assert.False(await db.PairAsync(invitation.Ticket, Credential, default));
            Assert.False((await db.InviteAsync(Target, false, default)).IsSuccess);
        }
        using var reopened = Open();
        var first = await reopened.StatusAsync(Target, default);
        Assert.Equal("reporting", first!.State);
        Assert.Equal("server-01", first.Hostname);
        Assert.Equal(_clock.Now.UtcDateTime, first.FirstSeenAt);
        _clock.Now = _clock.Now.AddSeconds(91);
        Assert.Equal("overdue", (await reopened.StatusAsync(Target, default))!.State);
        _clock.Now = _clock.Now.AddMinutes(4);
        Assert.Equal("no-contact", (await reopened.StatusAsync(Target, default))!.State);
        Assert.NotNull(await reopened.ReportAsync(Credential, "server-01", "1.1", new(), default));
        var next = await reopened.StatusAsync(Target, default);
        Assert.Equal(first.FirstSeenAt, next!.FirstSeenAt);
        Assert.Equal(_clock.Now.UtcDateTime, next.LastSeenAt);
        Assert.Equal("1.1", next.Version);
    }

    [Fact]
    public async Task ExpiryReplacementRevocationAndInventoryRemovalInvalidateCredentials()
    {
        await AddCluster();
        using var db = Open();
        var invitation = (await db.InviteAsync(Target, false, default)).Value!;
        Assert.True(await db.PairAsync(invitation.Ticket, Credential, default));
        _clock.Now = _clock.Now.AddMinutes(15);
        Assert.Null(await db.ReportAsync(Credential, "node", "1", new(), default));
        Assert.Equal("not-enrolled", (await db.StatusAsync(Target, default))!.State);
        var replacement = (await db.InviteAsync(Target, true, default)).Value!;
        Assert.False(await db.PairAsync(invitation.Ticket, Credential, default));
        Assert.True(await db.PairAsync(replacement.Ticket, OtherCredential, default));
        Assert.NotNull(await db.ReportAsync(OtherCredential, "node", "1", new(), default));
        Assert.True(await db.RevokeAsync(Target, default));
        Assert.Null(await db.ReportAsync(OtherCredential, "node", "1", new(), default));
        var latest = (await db.InviteAsync(Target, false, default)).Value!;
        Assert.True(await db.PairAsync(latest.Ticket, Credential, default));
        await _inventory.UpdateAsync(s => s with { Platforms = [] });
        await AddCluster();
        Assert.Null(await db.ReportAsync(Credential, "node", "1", new(), default));
    }

    [Fact]
    public async Task UsageHistoryKeepsPeaksMissingValuesAndGapsAcrossRetentions()
    {
        await AddCluster();
        using var db = Open();
        await Pair();
        AgentTelemetry Reading(double? cpu, double? peak) => new(cpu, peak, 1000, 700,
            [new("root", "/", 2000, 500)], [], _clock.Now.UtcDateTime);
        async Task Send(AgentTelemetry reading) => Assert.NotNull(await db.ReportAsync(
            Credential, "node", "2", new(2, reading), default));
        var original = Reading(20, 52);
        await Send(original);
        _clock.Now = _clock.Now.AddSeconds(30);
        await Send(Reading(40, 48));
        await Send(original); // delayed duplicate must not contribute another sample
        _clock.Now = _clock.Now.AddMinutes(12);
        await Send(Reading(null, null));
        var latest = await db.StatusAsync(Target, default);
        Assert.Null(latest!.Telemetry!.CpuAveragePercent);
        Assert.Equal(_clock.Now.UtcDateTime, latest.TelemetryAt);
        var fine = await db.HistoryAsync(Target, 1, default);
        Assert.Equal(3, fine!.Points.Count);
        Assert.Null(fine.Points[0].GpuUsagePercent);
        Assert.Equal(75d, fine.Points[0].DiskUsedPercent);
        Assert.Equal(30d, fine.Points[0].MemoryUsedPercent!.Value, 8);
        _clock.Now = _clock.Now.AddDays(2);
        db.PruneHistory();
        Assert.Empty((await db.HistoryAsync(Target, 24, default))!.Points);
        var rollup = Assert.Single((await db.HistoryAsync(Target, 168, default))!.Points);
        Assert.Equal(30, rollup.CpuAveragePercent);
        Assert.Equal(52, rollup.CpuPeakPercent);
        _clock.Now = _clock.Now.AddDays(31);
        db.PruneHistory();
        Assert.Empty((await db.HistoryAsync(Target, 720, default))!.Points);

        async Task Pair()
        {
            var ticket = (await db.InviteAsync(Target, false, default)).Value!.Ticket;
            Assert.True(await db.PairAsync(ticket, Credential, default));
        }
    }

    [Fact]
    public async Task FirstGenerationDatabaseOpensWithoutReenrolment()
    {
        await AddCluster();
        using (var old = new LiteDB.LiteDatabase(Path.Combine(_directory, "agent-reporting.db")) { UtcDate = true })
        {
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(Credential));
            old.GetCollection("bindings").Insert(new LiteDB.BsonDocument
            {
                ["_id"] = $"{_platformId:N}/{_nodeId:N}", ["PlatformId"] = _platformId, ["NodeId"] = _nodeId,
                ["CredentialHash"] = Convert.ToHexString(hash), ["Version"] = "legacy", ["Hostname"] = "old-host",
                ["FirstSeenAt"] = _clock.Now.UtcDateTime, ["LastSeenAt"] = _clock.Now.UtcDateTime
            });
        }
        using var migrated = Open();
        var existing = await migrated.StatusAsync(Target, default);
        Assert.Equal("reporting", existing!.State);
        Assert.False(existing.SupportsAssessment);
        Assert.Null(existing.Telemetry);
        Assert.NotNull(await migrated.ReportAsync(Credential, "old-host", "2", new(2), default));
        var updated = await migrated.StatusAsync(Target, default);
        Assert.Equal(existing.FirstSeenAt, updated!.FirstSeenAt);
        Assert.True(updated.SupportsAssessment);
    }

    [Fact]
    public async Task TelemetryUsesServerClockAndAnInvalidOptionalReadingDoesNotHideAnOnlineHost()
    {
        await AddCluster();
        using var db = Open();
        var ticket = (await db.InviteAsync(Target, false, default)).Value!.Ticket;
        Assert.True(await db.PairAsync(ticket, Credential, default));
        var skewed = _clock.Now.AddDays(-12).AddTicks(123).UtcDateTime;
        var valid = new AgentTelemetry(4, 6, 100, 60, [], [], skewed);
        Assert.NotNull(await db.ReportAsync(Credential, "pi", "2", new(2, valid), default));
        var observed = _clock.Now.UtcDateTime;
        _clock.Now = _clock.Now.AddMinutes(3);
        Assert.NotNull(await db.ReportAsync(Credential, "pi", "2", new(2, valid), default)); // retry
        var bad = valid with { CpuAveragePercent = 150 };
        Assert.NotNull(await db.ReportAsync(Credential, "pi", "2", new(2, bad), default));
        var latest = await db.StatusAsync(Target, default);
        Assert.Equal("reporting", latest!.State);
        Assert.Equal(_clock.Now.UtcDateTime, latest.LastSeenAt);
        Assert.Equal(observed, latest.TelemetryAt);
        var history = await db.HistoryAsync(Target, 1, default);
        Assert.Equal(observed, Assert.Single(history!.Points).At);
    }

    [Fact]
    public async Task UpdateCheckSurvivesRestartAndDeduplicatesLostDeliveries()
    {
        await AddCluster();
        Guid id;
        using (var db = Open())
        {
            var ticket = (await db.InviteAsync(Target, false, default)).Value!.Ticket;
            Assert.True(await db.PairAsync(ticket, Credential, default));
            Assert.False(await db.IsActiveCredentialAsync(Credential, default));
            Assert.NotNull(await db.ReportAsync(Credential, "node", "legacy", new(), default));
            Assert.True(await db.IsActiveCredentialAsync(Credential, default));
            Assert.False((await db.RequestCheckAsync(Target, default)).IsSuccess);
            Assert.NotNull(await db.ReportAsync(Credential, "node", "2", new(2), default));
            id = (await db.RequestCheckAsync(Target, default)).Value!.Id;
            Assert.Equal(id, (await db.RequestCheckAsync(Target, default)).Value!.Id);
            var delivery = await db.ReportAsync(Credential, "node", "2", new(2), default);
            Assert.Equal(new AgentJobDelivery(id, "check_updates"), delivery!.Job);
        }
        using var reopened = Open();
        var replay = await reopened.ReportAsync(Credential, "node", "2", new(2), default);
        Assert.Equal(id, replay!.Job!.Id);
        var assessment = new AgentAssessment(_clock.Now.UtcDateTime, _clock.Now.AddDays(-2).UtcDateTime,
            ["curl"], ["kernel"], 3, 1, false, null);
        var finished = new AgentCheckIn(2, Assessment: assessment, CompletedJobId: id);
        Assert.Null((await reopened.ReportAsync(Credential, "node", "2", finished, default))!.Job);
        var status = await reopened.StatusAsync(Target, default);
        Assert.Equal("succeeded", status!.CheckJob!.State);
        Assert.Equal("curl", Assert.Single(status.Assessment!.Pending));
        var nextId = (await reopened.RequestCheckAsync(Target, default)).Value!.Id;
        Assert.NotEqual(id, nextId);
        Assert.Equal(nextId, (await reopened.ReportAsync(Credential, "node", "2", finished, default))!.Job!.Id);
        _clock.Now = _clock.Now.AddMinutes(16);
        Assert.Equal("expired", (await reopened.StatusAsync(Target, default))!.CheckJob!.State);
        Assert.Null((await reopened.ReportAsync(Credential, "node", "2", new(2), default))!.Job);
        Assert.True(await reopened.RevokeAsync(Target, default));
        Assert.False(await reopened.IsActiveCredentialAsync(Credential, default));
    }

    [Fact]
    public async Task LatestRequestedCheckReplacesThePriorResultAfterTheNodeClockMovesBackwards()
    {
        await AddCluster();
        using var db = Open();
        var ticket = (await db.InviteAsync(Target, false, default)).Value!.Ticket;
        Assert.True(await db.PairAsync(ticket, Credential, default));
        await db.ReportAsync(Credential, "node", "2", new(2), default);

        async Task<AgentCheckIn> CompleteCheck(AgentAssessment assessment)
        {
            var id = (await db.RequestCheckAsync(Target, default)).Value!.Id;
            var delivery = await db.ReportAsync(Credential, "node", "2", new(2), default);
            Assert.Equal(id, delivery!.Job!.Id);
            var result = new AgentCheckIn(2, Assessment: assessment, CompletedJobId: id);
            await db.ReportAsync(Credential, "node", "2", result, default);
            return result;
        }

        var prior = new AgentAssessment(_clock.Now.UtcDateTime, null, [], [], null, null, null, "No APT indexes.");
        var earlierJob = await CompleteCheck(prior);
        _clock.Now = _clock.Now.AddMinutes(1);
        var correctedTime = _clock.Now.AddHours(-2).UtcDateTime;
        var fresh = await CompleteCheck(new(correctedTime, null, ["curl"], [], 1, 0, false, null));
        await db.ReportAsync(Credential, "node", "2", earlierJob, default); // stale completion retried

        var status = (await db.StatusAsync(Target, default))!;
        Assert.Equal(fresh.CompletedJobId, status.CheckJob!.Id);
        Assert.Equal("succeeded", status.CheckJob.State);
        Assert.Equal(correctedTime, status.Assessment!.CheckedAt);
        Assert.Null(status.Assessment.Error);
        Assert.Equal("curl", Assert.Single(status.Assessment.Pending));
    }

    [Fact]
    public void TransportIdentityIsStableAcrossRestarts()
    {
        var options = new JsonFuseStoreOptions { DataDirectory = _directory };
        using var first = new AgentServerIdentity(options);
        using var reopened = new AgentServerIdentity(options);
        Assert.Equal(first.Pin, reopened.Pin);
        Assert.True(reopened.Certificate.HasPrivateKey);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
