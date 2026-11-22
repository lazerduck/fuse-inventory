using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text.Json;
using Fuse.Agent;

[assembly: SupportedOSPlatform("linux")]

return await AgentProgram.MainAsync(args);

internal static class AgentProgram
{
    // The service manager supplies its private, read-only copy of the node credential.
    private static string AckPath => Path.Combine(
        Environment.GetEnvironmentVariable("RUNTIME_DIRECTORY") ?? "/run/fuse-agent", "last-checkin");
    private const string ServiceName = "fuse-agent.service";
    private static readonly string Version = typeof(AgentProgram).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";

    internal static async Task<int> MainAsync(string[] args)
    {
        using var shutdown = new CancellationTokenSource();
        using var term = PosixSignalRegistration.Create(PosixSignal.SIGTERM, signal =>
        {
            signal.Cancel = true;
            shutdown.Cancel();
        });
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };
        try
        {
            switch (args)
            {
                case ["preflight", var configFile]:
                    await PairAsync(configFile, shutdown.Token);
                    return 0;
                case ["run"]:
                    var secrets = Environment.GetEnvironmentVariable("CREDENTIALS_DIRECTORY") ??
                        throw new InvalidDataException("Start the worker using fuse-agent start.");
                    await RunAsync(Path.Combine(secrets, "node.json"), shutdown.Token);
                    return 0;
                case ["status"]:
                    var status = await Systemctl("is-active", ServiceName);
                    if (File.Exists(AckPath))
                        Console.WriteLine($"Last acknowledged check-in (UTC): {File.GetLastWriteTimeUtc(AckPath):O}");
                    Console.WriteLine($"Version: {Version}");
                    return status;
                case ["start"]:
                    return await Systemctl("enable", "--now", ServiceName);
                case ["stop"]:
                    return await Systemctl("disable", "--now", ServiceName);
                case ["upgrade"]:
                    return await AgentUpgrade.RunAsync(null, shutdown.Token);
                case ["upgrade", "--package", var packageFile]:
                    return await AgentUpgrade.RunAsync(packageFile, shutdown.Token);
                case ["--version"]:
                    Console.WriteLine(Version);
                    return 0;
                default:
                    Console.Error.WriteLine("Usage: fuse-agent {start|stop|status|upgrade|--version}");
                    return 2;
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { return args is ["run"] ? 0 : 130; }
        catch (Exception ex)
        {
            Console.Error.WriteLine(Diagnostic(ex));
            return 1;
        }
    }

    private static async Task PairAsync(string configFile, CancellationToken ct)
    {
        var line = await Console.In.ReadLineAsync(ct);
        if (string.IsNullOrEmpty(line) || line.Length > 8192)
            throw new InvalidDataException("Expected a pairing bundle on standard input.");
        var decoded = Convert.FromBase64String(line.Replace('-', '+').Replace('_', '/').PadRight((line.Length + 3) / 4 * 4, '='));
        var bundle = JsonSerializer.Deserialize<PairingBundle>(decoded, AgentConnection.Json)
            ?? throw new InvalidDataException("The pairing bundle is empty.");
        if (bundle.Url is null || string.IsNullOrEmpty(bundle.Pin) || string.IsNullOrEmpty(bundle.Ticket))
            throw new InvalidDataException("The pairing bundle is incomplete.");
        AgentConnection.ValidateEndpoint(bundle.Url, bundle.Pin);
        var credential = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var identity = new NodeIdentity(bundle.Url, bundle.Pin, credential);
        var request = new { type = "pair", ticket = bundle.Ticket, credential };
        try
        {
            await AgentConnection.ExchangeAsync(identity, request, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is IOException or OperationCanceledException)
        {
            // Reuse the credential if Fuse reserved the ticket but its first acknowledgement was lost.
            await AgentConnection.ExchangeAsync(identity, request, ct);
        }

        await using var config = new FileStream(configFile, new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        });
        await JsonSerializer.SerializeAsync(config, identity, AgentConnection.Json, ct);
        Console.WriteLine("Authenticated preflight acknowledged by Fuse.");
    }

    private static async Task RunAsync(string configFile, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(configFile, ct);
        var identity = JsonSerializer.Deserialize<NodeIdentity>(bytes, AgentConnection.Json)
            ?? throw new InvalidDataException("Missing node identity.");
        AgentConnection.ValidateEndpoint(identity.Url, identity.Pin);
        var sampler = new HostTelemetry();
        sampler.SampleCpu();
        var assessments = new AssessmentSchedule(Environment.GetEnvironmentVariable("STATE_DIRECTORY")
            ?? Path.GetDirectoryName(AckPath)!);
        var connectionLog = new ConnectionLog(TimeProvider.System, Console.Error);
        int backoff = 1;
        while (!ct.IsCancellationRequested)
        {
            var reported = false;
            try
            {
                reported = await ReportAsync(identity, sampler, assessments, connectionLog, ct);
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested) return;
                Console.Error.WriteLine(Diagnostic(ex));
            }
            TimeSpan delay;
            if (reported)
            {
                delay = TimeSpan.FromSeconds(30 + Random.Shared.Next(-2, 3));
                backoff = 1;
            }
            else
            {
                delay = TimeSpan.FromSeconds(backoff + Random.Shared.NextDouble());
                backoff = Math.Min(60, backoff * 2);
            }
            await sampler.WaitAndSampleAsync(delay, ct);
        }
    }

    private static async Task<bool> ReportAsync(NodeIdentity identity, HostTelemetry sampler,
        AssessmentSchedule assessments, ConnectionLog connectionLog, CancellationToken ct)
    {
        await assessments.TickAsync(ct);
        var telemetry = await sampler.CaptureAsync(ct);
        var request = new WorkerCheckIn(identity.Credential, Environment.MachineName, Version, telemetry,
            assessments.Report, assessments.CompletedJobId).Encode();
        AgentReply reply;
        try
        {
            reply = await AgentConnection.ExchangeAsync(identity, request, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            connectionLog.Failed(Diagnostic(ex));
            return false;
        }
        connectionLog.Acknowledged();
        assessments.Acknowledged();
        if (reply.Job is { Type: "check_updates" } job && job.Id != Guid.Empty)
        {
            assessments.Requested(job.Id);
            await assessments.TickAsync(ct);
        }
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        await File.WriteAllTextAsync(AckPath + ".tmp", timestamp, ct);
        File.Move(AckPath + ".tmp", AckPath, overwrite: true);
        return true;
    }

    private static string Diagnostic(Exception ex) => ex switch
    {
        AuthenticationException => "The encrypted connection failed. Verify that the pairing command belongs to this Fuse instance.",
        OperationCanceledException => "Timed out waiting for Fuse.",
        _ => ex.Message
    };

    private static async Task<int> Systemctl(params string[] arguments)
    {
        var start = new ProcessStartInfo("/usr/bin/systemctl") { UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start systemctl.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
