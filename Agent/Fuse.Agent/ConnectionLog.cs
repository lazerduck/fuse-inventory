namespace Fuse.Agent;

internal sealed class ConnectionLog(TimeProvider clock, TextWriter journal)
{
    private long? lastFailureLog;

    internal void Failed(string reason)
    {
        var now = clock.GetTimestamp();
        if (lastFailureLog is { } previous)
        {
            if (clock.GetElapsedTime(previous, now) < TimeSpan.FromHours(1)) return;
            journal.WriteLine($"Still unable to check in with Fuse. Retrying. {reason}");
        }
        else
            journal.WriteLine($"Unable to check in with Fuse. Retrying. {reason}");

        lastFailureLog = now;
    }

    internal void Acknowledged()
    {
        if (lastFailureLog is null) return;
        journal.WriteLine("Reconnected to Fuse. Check-in acknowledged.");
        lastFailureLog = null;
    }
}
