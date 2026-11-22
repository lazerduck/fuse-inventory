using System.Text.Json;

namespace Fuse.Agent;

internal sealed record AptAssessment(DateTimeOffset CheckedAt, DateTimeOffset? IndexUpdatedAt,
    List<string> Pending, List<string> Held, int? PendingCount, int? HeldCount, bool? RebootRequired, string? Error);

internal static class AptInspection
{
    private const string Lists = "/var/lib/apt/lists";
    internal const string Installed = "/var/lib/dpkg/status";

    internal static Task<AptAssessment> InspectAsync(CancellationToken ct) => InspectAsync(Lists, ct);

    internal static async Task<AptAssessment> InspectAsync(string listsDirectory, CancellationToken ct)
    {
        var packages = new PackageList();
        string? error = null;
        try
        {
            if (!Directory.EnumerateFiles(listsDirectory, "*_Packages*").Any())
                throw new IOException("No local APT package indexes are available.");
            await CommandOutput.ReadAsync("/usr/bin/apt-get", ["--simulate", "--with-new-pkgs", "upgrade"],
                packages.ReadSimulationLine, TimeSpan.FromSeconds(120), ct);
            await CommandOutput.ReadAsync("/usr/bin/apt-mark", ["showhold"], packages.AddHold, TimeSpan.FromSeconds(30), ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            error = new string(ex.Message.Take(240).Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
            if (error.Length == 0) error = "Unable to assess local APT state.";
        }

        bool? reboot = File.Exists("/run/reboot-required") ? true :
            File.Exists("/usr/share/update-notifier/notify-reboot-required") ||
            Directory.Exists("/usr/share/doc/reboot-notifier") ? false : null;
        // Repository lists carry publisher timestamps. Only the success stamp establishes when the host refreshed.
        var indexTime = Timestamp("/var/lib/apt/periodic/update-success-stamp");
        return new(DateTimeOffset.UtcNow, indexTime,
            error is null ? packages.Pending.Order(StringComparer.Ordinal).Take(100).ToList() : [],
            error is null ? packages.Held.Order(StringComparer.Ordinal).Take(100).ToList() : [],
            error is null ? packages.Pending.Count : null, error is null ? packages.Held.Count : null, reboot, error);
    }

    internal static DateTimeOffset? Timestamp(string path)
        => File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;

}

internal sealed class PackageList
{
    internal HashSet<string> Pending { get; } = new(StringComparer.Ordinal);
    internal HashSet<string> Held { get; } = new(StringComparer.Ordinal);
    private bool readingHeld;

    internal void ReadSimulationLine(string line)
    {
        if (line.StartsWith("Inst ", StringComparison.Ordinal))
        {
            var name = line[5..].Split(' ', 2)[0];
            if (ValidName(name)) Pending.Add(name);
        }
        if (readingHeld && line.StartsWith(' '))
        {
            foreach (var name in line.Split(' ', StringSplitOptions.RemoveEmptyEntries)) AddHold(name);
        }
        else
            readingHeld = line == "The following packages have been kept back:" ||
                          line == "The following upgrades have been deferred due to phasing:";
    }

    internal void AddHold(string name)
    {
        if (ValidName(name)) Held.Add(name);
    }

    private static bool ValidName(string name) => name.Length is > 0 and <= 128 &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '+' or '.' or ':');
}

// APT inspection runs independently from the regular heartbeats, including on slower hosts.
internal sealed class AssessmentSchedule
{
    internal sealed record Completion(Guid JobId, AptAssessment Assessment);

    private readonly string lastJobFile;
    private readonly Func<CancellationToken, Task<AptAssessment>> inspect;
    private Task<AptAssessment>? running;
    private Guid? runningJob, requested;
    private DateTimeOffset? installedAtStart, nextDue, lastStarted;
    private Completion? previousJob;
    internal AptAssessment? Report { get; private set; }
    internal Guid? CompletedJobId { get; private set; }

    internal AssessmentSchedule(string stateDirectory, Func<CancellationToken, Task<AptAssessment>>? inspect = null)
    {
        this.inspect = inspect ?? AptInspection.InspectAsync;
        lastJobFile = Path.Combine(stateDirectory, "apt-job.json");
        try
        {
            if (File.Exists(lastJobFile))
                previousJob = JsonSerializer.Deserialize<Completion>(File.ReadAllBytes(lastJobFile), AgentConnection.Json);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        { /* A read-only check can be repeated safely. */ }
    }

    internal void Requested(Guid id)
    {
        if (previousJob?.JobId == id)
        {
            Report = previousJob.Assessment;
            CompletedJobId = id;
        }
        else if (runningJob != id)
            requested = id;
    }

    internal async Task TickAsync(CancellationToken ct)
    {
        if (running?.IsCompleted == true)
        {
            Report = await running;
            CompletedJobId = runningJob;
            if (runningJob is { } id)
            {
                previousJob = new(id, Report);
                try
                {
                    await File.WriteAllTextAsync(lastJobFile + ".tmp",
                        JsonSerializer.Serialize(previousJob, AgentConnection.Json), ct);
                    File.Move(lastJobFile + ".tmp", lastJobFile, true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { Console.Error.WriteLine("Could not persist the completed APT check. Its result remains available in memory."); }
            }
            // Retry failed reads sooner than the six-hour refresh, while avoiding busy loops.
            nextDue = DateTimeOffset.UtcNow + (Report.Error is null ? TimeSpan.FromHours(6) : TimeSpan.FromMinutes(15));
            running = null;
            runningJob = null;
        }
        if (running is not null || Report is not null) return;
        var installed = AptInspection.Timestamp(AptInspection.Installed);
        var now = DateTimeOffset.UtcNow;
        if (requested is null && (now < lastStarted?.AddMinutes(1) ||
            (now < nextDue && installedAtStart == installed))) return;
        installedAtStart = installed;
        lastStarted = now;
        runningJob = requested;
        requested = null;
        running = inspect(ct);
    }

    internal void Acknowledged()
    {
        Report = null;
        CompletedJobId = null;
    }
}
