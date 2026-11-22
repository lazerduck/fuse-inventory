using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Fuse.Agent;

internal static class AgentUpgrade
{
    [DllImport("libc")]
    private static extern uint geteuid();

    internal static async Task<int> RunAsync(string? suppliedPackage, CancellationToken ct)
    {
        if (geteuid() != 0) throw new InvalidOperationException("Run sudo fuse-agent upgrade on the enrolled server.");
        const string journal = "/var/lib/fuse-agent-upgrade";
        if (File.Exists(Path.Combine(journal, "pending")))
            return await RunHelperAsync(Path.Combine(journal, "recover.sh"), "--recover");
        const string config = "/etc/fuse-agent/node.json";
        var identity = JsonSerializer.Deserialize<NodeIdentity>(await File.ReadAllBytesAsync(config, ct), AgentConnection.Json)
            ?? throw new InvalidDataException("The node has no paired Fuse identity.");
        AgentConnection.ValidateEndpoint(identity.Url, identity.Pin);
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "amd64", Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException("Supported package architectures: amd64, arm64.")
        };

        Console.WriteLine("Checking the worker release with the paired Fuse instance...");
        var reply = await AgentConnection.ExchangeAsync(identity, new { type = "release", credential = identity.Credential, architecture }, ct);
        var release = reply.Release;
        if (release is null || release.Sha256 is not { Length: 64 } || !release.Sha256.All(char.IsAsciiHexDigit) ||
            string.IsNullOrEmpty(release.Version) || release.Version.Length > 100 ||
            !char.IsAsciiDigit(release.Version[0]) || !release.Version.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '+' or '~' or ':'))
            throw new InvalidDataException("Fuse did not advertise a valid package release.");
        var current = "";
        await CommandOutput.ReadAsync("/usr/bin/dpkg-query", ["-W", "-f=${Version}", "fuse-agent"], line => current = line,
            TimeSpan.FromSeconds(10), ct);
        if (current == release.Version)
        {
            Console.WriteLine($"The installed worker is already at {current}.");
            return 0;
        }

        var staging = Directory.CreateDirectory(Path.Combine("/var/tmp", "fuse-agent-upgrade-" + Guid.NewGuid().ToString("N")),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            var package = Path.Combine(staging.FullName, "agent.deb");
            if (suppliedPackage is null)
            {
                var uri = new UriBuilder(new Uri(identity.Url, $"packages/fuse-agent-{architecture}.deb"))
                    { Scheme = identity.Url.Scheme == "ws" ? "http" : "https", Port = identity.Url.Port };
                Console.WriteLine($"Downloading the {architecture} worker package for {release.Version}...");
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromMinutes(5));
                using var response = await http.GetAsync(uri.Uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
                await using var output = File.Create(package);
                var buffer = new byte[81920];
                long received = 0;
                int size;
                while ((size = await input.ReadAsync(buffer, deadline.Token)) > 0)
                {
                    received += size;
                    if (received > 256L * 1024 * 1024) throw new InvalidDataException("Package exceeded its size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, size), deadline.Token);
                }
            }
            else File.Copy(suppliedPackage, package);
            await using (var input = File.OpenRead(package))
            {
                var hash = await SHA256.HashDataAsync(input, ct);
                if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(release.Sha256)))
                    throw new InvalidDataException("The downloaded worker differs from the release verified by the paired Fuse instance.");
            }

            var script = Path.Combine(staging.FullName, "upgrade.sh");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "upgrade.sh"), script);
            return await RunHelperAsync(script, package, architecture, release.Version);
        }
        finally { staging.Delete(true); }
    }

    private static async Task<int> RunHelperAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Could not start local package installation.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
