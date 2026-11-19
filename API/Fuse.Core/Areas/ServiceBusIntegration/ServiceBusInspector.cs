using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.ServiceBusIntegration;

public sealed partial class ServiceBusInspector(HttpClient http, IAzureIntegrationManagerService manager, ITokenCredentialFactory? factory = null)
{
    private const string Arm = "https://management.azure.com";
    private const string Version = "2024-01-01";
    private static readonly TokenRequestContext TokenScope = new(["https://management.azure.com/.default"]);
    private readonly ITokenCredentialFactory credentials = factory ?? new DefaultTokenCredentialFactory();

    [GeneratedRegex(@"\A/subscriptions/([a-f0-9-]{36})/resourceGroups/([\p{L}\p{N}_.()\-]{1,90})/providers/Microsoft\.ServiceBus/namespaces/[a-z][a-z0-9-]{4,48}[a-z0-9]/?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamespacePattern();

    internal static bool Valid(string? resourceId, SecretProviderAuthMode mode) =>
        resourceId is { Length: < 512 } && Enum.IsDefined(mode) && NamespacePattern().Match(resourceId) is { Success: true } match
        && Guid.TryParseExact(match.Groups[1].Value, "D", out _) && !match.Groups[2].Value.EndsWith('.');

    private async Task<string> GetToken(SecretProviderAuthMode mode, CancellationToken ct)
    {
        var shared = mode == SecretProviderAuthMode.ClientSecret ? await manager.GetClientSecretCredentialsAsync() : null;
        if (mode == SecretProviderAuthMode.ClientSecret && (string.IsNullOrWhiteSpace(shared?.TenantId) ||
            string.IsNullOrWhiteSpace(shared.ClientId) || string.IsNullOrWhiteSpace(shared.ClientSecret)))
            throw new InspectionError("credentials-not-configured");
        return (await credentials.Create(mode, shared).GetTokenAsync(TokenScope, ct)).Token;
    }

    private sealed class InspectionError(string status) : Exception { public string Status { get; } = status; }
    private static string? Failure(Exception e, CancellationToken caller) => e switch
    {
        InspectionError failure => failure.Status,
        AuthenticationFailedException or ArgumentException => "authentication-failed",
        OperationCanceledException when !caller.IsCancellationRequested => "timeout",
        HttpRequestException => "unavailable",
        JsonException or InvalidOperationException or KeyNotFoundException => "invalid-response",
        _ => null
    };

    private sealed record PageList<T>(List<T> Items, string Status);
    private sealed record QueueMetadata(string Path, string Name, string? Status);

    private sealed class ReadSession(HttpClient http, string token, CancellationToken caller, CancellationToken deadline)
    {
        private int remaining = 300;

        private async Task<JsonDocument> Read(Uri next)
        {
            deadline.ThrowIfCancellationRequested();
            if (remaining-- <= 0) throw new InspectionError("limit-reached");
            using var request = new HttpRequestMessage(HttpMethod.Get, next);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, deadline);
            if (!response.IsSuccessStatusCode) throw new InspectionError(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "authentication-failed", HttpStatusCode.Forbidden => "denied",
                HttpStatusCode.NotFound => "not-found-or-hidden", HttpStatusCode.TooManyRequests => "throttled",
                _ => "request-failed"
            });
            return JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(deadline));
        }

        public async Task<QueueMetadata?> KnownQueue(string namespaceId, string name)
        {
            var segment = name.Replace('/', '~');
            if (!ValidQueueSegment(segment)) return null;
            var path = namespaceId + "/queues/" + segment;
            try
            {
                using var json = await Read(new Uri(Arm + path + "?api-version=" + Version));
                var queue = Queue(namespaceId, json.RootElement);
                return queue.Path.Equals(path, StringComparison.OrdinalIgnoreCase) ? queue : null;
            }
            catch (Exception e) when (Failure(e, caller) is not null) { return null; }
        }

        public async Task<PageList<T>> List<T>(string path, Func<JsonElement, T> project, bool firstPageOnly = false)
        {
            var rows = new List<T>();
            var first = new Uri(Arm + path + "?api-version=" + Version);
            Uri? next = first;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                while (next is not null)
                {
                    deadline.ThrowIfCancellationRequested();
                    if (visited.Count >= 100) throw new InspectionError("limit-reached");
                    if (next.Scheme != first.Scheme || next.Authority != first.Authority || next.UserInfo.Length != 0 ||
                        next.Fragment.Length != 0 || !next.AbsolutePath.Equals(first.AbsolutePath, StringComparison.OrdinalIgnoreCase)
                        || !visited.Add(next.AbsoluteUri)) throw new InspectionError("invalid-continuation");
                    using var json = await Read(next);
                    var root = json.RootElement;
                    foreach (var entry in root.GetProperty("value").EnumerateArray())
                    {
                        if (rows.Count >= 1000) throw new InspectionError("limit-reached");
                        rows.Add(project(entry));
                    }
                    var link = Text(root, "nextLink");
                    if (firstPageOnly || string.IsNullOrEmpty(link)) break;
                    if (!Uri.TryCreate(link, UriKind.Absolute, out next)) throw new InspectionError("invalid-continuation");
                }
                return new(rows, "visible");
            }
            catch (Exception e) when (Failure(e, caller) is { } status) { return new(rows, status); }
        }

        public async Task<ServiceBusPolicyList> Policies(string path)
        {
            var list = await List(path + "/authorizationRules", row =>
            {
                var rights = row.GetProperty("properties").GetProperty("rights").EnumerateArray()
                    .Select(r => r.GetString() ?? throw new InspectionError("invalid-response")).ToArray();
                return new ServiceBusPolicy(Required(row, "name"), rights);
            });
            return new(list.Status, list.Items.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }

    private static string? Text(JsonElement row, string key) =>
        row.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
    private static string Required(JsonElement row, string key) =>
        Text(row, key) is { Length: > 0 } s ? s : throw new InspectionError("invalid-response");

    private static bool ValidQueueSegment(string segment) => segment.Length is > 0 and <= 260
        && segment is not ("." or "..") && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~');

    private static QueueMetadata Queue(string namespaceId, JsonElement row)
    {
        var id = Required(row, "id");
        var prefix = namespaceId + "/queues/";
        if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InspectionError("invalid-response");
        var segment = id[prefix.Length..];
        if (!ValidQueueSegment(segment))
            throw new InspectionError("invalid-response");
        return new(prefix + segment, Required(row, "name"), Text(row.GetProperty("properties"), "status"));
    }

    public async Task<ServiceBusConnectionTestResult> ValidateAsync(string namespaceId, SecretProviderAuthMode mode, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var session = new ReadSession(http, await GetToken(mode, deadline.Token), ct, deadline.Token);
            var result = await session.List(namespaceId + "/queues", row => Queue(namespaceId, row), firstPageOnly: true);
            return new(result.Status == "visible", result.Status);
        }
        catch (Exception e) when (Failure(e, ct) is { } status) { return new(false, status); }
    }

    public async Task<ServiceBusDiscoveryResponse> DiscoverAsync(ServiceBusConnection item, IReadOnlyList<BrokerQueue> expected, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var observed = DateTimeOffset.UtcNow;
        var queues = new List<ServiceBusQueue>();
        ServiceBusPolicyList namespacePolicies = new("unknown", []);
        string queueVisibility;
        try
        {
            var session = new ReadSession(http, await GetToken(item.AuthMode, deadline.Token), ct, deadline.Token);
            var list = await session.List(item.NamespaceResourceId + "/queues", row => Queue(item.NamespaceResourceId, row));
            queueVisibility = list.Status;
            namespacePolicies = await session.Policies(item.NamespaceResourceId);
            var visible = list.Items.DistinctBy(q => q.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(q => q.Path, StringComparer.OrdinalIgnoreCase);
            // A Reader assignment on a single queue can allow a direct read while hiding the namespace listing.
            foreach (var name in expected.Select(q => q.Name).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                var path = item.NamespaceResourceId + "/queues/" + name.Replace('/', '~');
                if (!visible.ContainsKey(path) && await session.KnownQueue(item.NamespaceResourceId, name) is { } known)
                    visible.TryAdd(known.Path, known);
            }
            foreach (var queue in visible.Values.OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase))
                queues.Add(new(queue.Name, queue.Status, await session.Policies(queue.Path)));
        }
        catch (Exception e) when (Failure(e, ct) is { } status)
        {
            queueVisibility = status;
            namespacePolicies = new(status, []);
        }
        var names = queues.Select(q => q.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inventory = expected.Select(q => new ServiceBusQueueMatch(q.Id, q.Name, names.Contains(q.Name) || names.Contains(q.Name.Replace('/', '~')) ? "observed" : "unknown")).ToArray();
        return new(item.NamespaceResourceId, observed, queueVisibility, queues, namespacePolicies, inventory);
    }
}
