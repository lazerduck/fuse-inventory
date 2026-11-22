using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Fuse.AgentProtocol;

namespace Fuse.Agent;

internal sealed record PairingBundle(Uri Url, string Pin, string Ticket);
internal sealed record NodeIdentity(Uri Url, string Pin, string Credential);
internal sealed record AgentJob(Guid Id, string Type);
internal sealed record AgentRelease(string Version, string Sha256);
internal sealed record AgentReply(AgentJob? Job, AgentRelease? Release);
internal sealed record WorkerCheckIn(string Credential, string Hostname, string Version, TelemetryReading Telemetry,
    AptAssessment? Assessment, Guid? CompletedJobId)
{
    public string Type => "checkin";
    public int ProtocolVersion => 2;

    internal byte[] Encode()
    {
        var reading = Telemetry;
        while (true)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(this with { Telemetry = reading }, AgentConnection.Json);
            if (bytes.Length < 96 * 1024) return bytes;
            // UTF-8 JSON escaping can expand long non-ASCII filesystem names significantly.
            // Both lists of package names are separately bounded to 100 short ASCII identifiers.
            if (reading.Disks.Count == 0 && reading.Gpus.Count == 0)
                throw new InvalidDataException("The check-in exceeded its wire size limit.");
            reading = reading with
            {
                Disks = reading.Disks.Take(reading.Disks.Count / 2).ToList(),
                Gpus = reading.Gpus.Take(reading.Gpus.Count / 2).ToList()
            };
        }
    }
}

internal static class AgentConnection
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static void ValidateEndpoint(Uri uri, string pin)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("ws" or "wss") ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidDataException("Expected a WebSocket URL without credentials, a query or a fragment.");
        if (Convert.FromBase64String(pin).Length != SHA256.HashSizeInBytes)
            throw new InvalidDataException("Invalid Fuse identity in the pairing configuration.");
    }

    internal static Task<AgentReply> ExchangeAsync(NodeIdentity identity, object message, CancellationToken ct) =>
        ExchangeAsync(identity, JsonSerializer.SerializeToUtf8Bytes(message, Json), ct);

    internal static async Task<AgentReply> ExchangeAsync(NodeIdentity identity, byte[] request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var ct = timeout.Token;
        using var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(identity.Url, ct);
        }
        catch (WebSocketException ex)
        {
            throw new IOException("Cannot open the agent WebSocket. Check the Fuse address and proxy Upgrade forwarding.", ex);
        }

        using var transport = new WebSocketByteStream(socket);
        await using var encrypted = new SslStream(transport, leaveInnerStreamOpen: true);
        var pin = Convert.FromBase64String(identity.Pin);
        await encrypted.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = identity.Url.Host,
            EnabledSslProtocols = SslProtocols.Tls13,
            RemoteCertificateValidationCallback = (_, cert, _, _) =>
            {
                if (cert is not X509Certificate2 serverCertificate)
                    return false;
                var actual = SHA256.HashData(serverCertificate.PublicKey.ExportSubjectPublicKeyInfo());
                return CryptographicOperations.FixedTimeEquals(actual, pin);
            }
        }, ct);

        await encrypted.WriteAsync(request, ct);
        await encrypted.WriteAsync("\n"u8.ToArray(), ct);
        await encrypted.FlushAsync(ct);

        // The protocol has one small newline-delimited reply per connection.
        byte[] buffer = new byte[4096];
        int offset = 0;
        while (offset < buffer.Length)
        {
            int count = await encrypted.ReadAsync(buffer.AsMemory(offset), ct);
            if (count == 0)
                throw new IOException("Fuse closed the connection before acknowledging the request.");
            var newline = Array.IndexOf(buffer, (byte)'\n', offset, count);
            if (newline >= 0)
            {
                using var reply = JsonDocument.Parse(buffer.AsMemory(0, newline));
                if (reply.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True)
                    return reply.RootElement.Deserialize<AgentReply>(Json) ?? throw new InvalidDataException("Invalid Fuse reply.");
                var reason = reply.RootElement.TryGetProperty("error", out var error) &&
                    error.ValueKind == JsonValueKind.String ? error.GetString() : "the request was declined";
                throw new InvalidDataException($"Fuse declined the request: {reason}");
            }
            offset += count;
        }
        throw new InvalidDataException("The Fuse acknowledgement exceeded its size limit.");
    }
}
