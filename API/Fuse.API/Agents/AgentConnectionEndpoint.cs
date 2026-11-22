using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Text.Json;
using Fuse.AgentProtocol;
using Fuse.Core.Areas.PlatformAgent;

namespace Fuse.API.Agents;

public sealed class AgentConnectionEndpoint(
    AgentServerIdentity identity, IAgentReportingStore store, AgentReleaseAssets releases, ILogger<AgentConnectionEndpoint> logger)
{
    private readonly SemaphoreSlim _sessions = new(64);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 8 };

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            return;
        }
        if (!_sessions.Wait(0))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            var ct = deadline.Token;
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            using var transport = new WebSocketByteStream(socket);
            using var tls = new SslStream(transport, leaveInnerStreamOpen: true);
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = identity.Certificate,
                EnabledSslProtocols = SslProtocols.Tls13
            }, ct);
            var request = await ReadRequestAsync(tls, ct);
            var reply = await ExchangeAsync(request, ct);
            await tls.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(reply, Json), ct);
            await tls.WriteAsync("\n"u8.ToArray(), ct);
            await tls.FlushAsync(ct);
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
        }
        catch (Exception ex) when (ex is AuthenticationException or IOException or WebSocketException or
            OperationCanceledException or JsonException)
        {
            // Do not log request bodies, invitations, or credentials.
            logger.LogDebug("Agent connection ended before completing the exchange ({Kind}).", ex.GetType().Name);
        }
        finally { _sessions.Release(); }
    }

    private async Task<object> ExchangeAsync(AgentRequest? request, CancellationToken ct)
    {
        switch (request)
        {
            case { Type: "pair", Ticket: { } ticket, Credential: { } credential }:
                if (await store.PairAsync(ticket, credential, ct)) return new { ok = true };
                break;
            case { Type: "checkin", Credential: { } credential, Hostname: { } name, Version: { } version }:
                var report = new AgentCheckIn(request.ProtocolVersion, request.Telemetry, request.Assessment, request.CompletedJobId);
                var result = await store.ReportAsync(credential, name, version, report, ct);
                if (result != null) return new { ok = true, result.Job };
                break;
            case { Type: "release", Credential: { } credential, Architecture: "amd64" or "arm64" }:
                if (!await store.IsActiveCredentialAsync(credential, ct)) break;
                var hashes = releases.GetChecksums();
                var releaseVersion = releases.GetAgentVersion();
                if (hashes == null || releaseVersion == null)
                    return new { ok = false, error = "This Fuse build has no compatible agent release." };
                return new { ok = true, release = new { version = releaseVersion,
                    sha256 = request.Architecture == "amd64" ? hashes.Amd64Sha256 : hashes.Arm64Sha256 } };
        }
        return new { ok = false, error = "Agent request rejected. Check the invitation, enrolment and report." };
    }

    private static async Task<AgentRequest?> ReadRequestAsync(Stream stream, CancellationToken ct)
    {
        const int maximum = 96 * 1024;
        var buffer = new byte[4096];
        var length = 0;
        while (length < maximum)
        {
            if (length == buffer.Length) Array.Resize(ref buffer, Math.Min(maximum, buffer.Length * 2));
            var count = await stream.ReadAsync(buffer.AsMemory(length), ct);
            if (count == 0) return null;
            var end = Array.IndexOf(buffer, (byte)'\n', length, count);
            if (end >= 0) return JsonSerializer.Deserialize<AgentRequest>(buffer.AsSpan(0, end), Json);
            length += count;
        }
        return null;
    }

    private sealed record AgentRequest(string? Type, string? Credential, string? Ticket, string? Hostname, string? Version,
        int ProtocolVersion = 1, AgentTelemetry? Telemetry = null, AgentAssessment? Assessment = null,
        Guid? CompletedJobId = null, string? Architecture = null);
}
