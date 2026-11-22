using Xunit;

namespace Fuse.Agent.Tests;

public sealed class ConnectionLogTests
{
    [Fact]
    public void RepeatedFailuresEmitOneReminderPerElapsedHour()
    {
        var clock = new ManualClock();
        using var journal = new StringWriter();
        var log = new ConnectionLog(clock, journal);

        log.Failed("Timeout.");
        clock.Advance(TimeSpan.FromMinutes(59));
        log.Failed("Different network error.");
        clock.Advance(TimeSpan.FromMinutes(1));
        log.Failed("Timeout.");
        log.Failed("Immediate retry.");
        clock.Advance(TimeSpan.FromHours(3));
        log.Failed("Fuse declined the request.");
        log.Failed("Immediate retry.");

        Assert.Equal(new[]
        {
            "Unable to check in with Fuse. Retrying. Timeout.",
            "Still unable to check in with Fuse. Retrying. Timeout.",
            "Still unable to check in with Fuse. Retrying. Fuse declined the request."
        }, Lines(journal));
    }

    [Fact]
    public void HealthyCheckInsAreSilentAndEachOutageHasOneRecovery()
    {
        var clock = new ManualClock();
        using var journal = new StringWriter();
        var log = new ConnectionLog(clock, journal);

        log.Acknowledged();
        log.Acknowledged();
        log.Failed("Offline.");
        log.Failed("Offline.");
        log.Acknowledged();
        log.Acknowledged();
        log.Failed("Offline again.");
        log.Acknowledged();

        Assert.Equal(new[]
        {
            "Unable to check in with Fuse. Retrying. Offline.",
            "Reconnected to Fuse. Check-in acknowledged.",
            "Unable to check in with Fuse. Retrying. Offline again.",
            "Reconnected to Fuse. Check-in acknowledged."
        }, Lines(journal));
    }

    private static string[] Lines(StringWriter journal) =>
        journal.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    private sealed class ManualClock : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public void Advance(TimeSpan by) => ticks += by.Ticks;
    }
}
