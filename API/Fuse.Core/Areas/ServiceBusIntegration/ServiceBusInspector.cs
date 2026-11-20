using System.Text.Json;
using System.Text.RegularExpressions;
using Fuse.Core.Areas.AzureMetadata;
using static Fuse.Core.Areas.AzureMetadata.ArmMetadataSession;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.ServiceBusIntegration;

public sealed partial class ServiceBusInspector(HttpClient http, IAzureIntegrationManagerService manager, ITokenCredentialFactory? factory = null)
{
    private const string Version = "2024-01-01";
    private readonly ITokenCredentialFactory credentials = factory ?? new DefaultTokenCredentialFactory();

    [GeneratedRegex(@"\A/subscriptions/([a-f0-9-]{36})/resourceGroups/([\p{L}\p{N}_.()\-]{1,90})/providers/Microsoft\.ServiceBus/namespaces/[a-z][a-z0-9-]{4,48}[a-z0-9]/?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamespacePattern();

    internal static bool Valid(string? resourceId, SecretProviderAuthMode mode) =>
        resourceId is { Length: < 512 } && Enum.IsDefined(mode) && NamespacePattern().Match(resourceId) is { Success: true } match
        && Guid.TryParseExact(match.Groups[1].Value, "D", out _) && !match.Groups[2].Value.EndsWith('.');

    private sealed record NamespaceAuth(bool? Enabled);
    private sealed record QueueMetadata(string Path, string Name, string? Status, string? Ttl, int? MaxDelivery, long? DeadLetters);

    private sealed class ReadSession(HttpClient http, string token, CancellationToken caller, CancellationToken deadline)
        : ArmMetadataSession(http, token, caller, deadline, Version)
    {
        public async Task<(QueueMetadata? Queue, string Status)> KnownQueue(string namespaceId, string name)
        {
            var segment = name.Replace('/', '~');
            if (!ValidQueueSegment(segment)) return (null, "unknown");
            var path = namespaceId + "/queues/" + segment;
            try
            {
                using var json = await Read(new Uri(Arm + path + "?api-version=" + Version));
                var queue = Queue(namespaceId, json.RootElement);
                return queue.Path.Equals(path, StringComparison.OrdinalIgnoreCase) ? (queue, "visible") : (null, "invalid-response");
            }
            catch (Exception e) when (Failure(e, Caller) is { } reason) { return (null, reason); }
        }

        public async Task<ServiceBusPolicyList> Policies(string path)
        {
            var list = await List(path + "/authorizationRules", row =>
            {
                var rights = row.GetProperty("properties").GetProperty("rights").EnumerateArray()
                    .Select(r => r.GetString() ?? throw new InspectionError("invalid-response")).ToArray();
                return new ServiceBusPolicy(Required(row, "name"), rights);
            });
            // ARM can return HTTP 200 with an empty authorization-rule list when the rules are hidden by RBAC.
            var visibility = list.Status == "visible" && list.Items.Count == 0 ? "unknown" : list.Status;
            return new(visibility, list.Items.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }

    internal async Task<ArmMetadataSession> Open(ServiceBusConnection item, CancellationToken caller, CancellationToken deadline) =>
        new(http, await GetToken(manager, credentials, item.AuthMode, deadline, item.CredentialProfileId, item.NamespaceResourceId), caller, deadline, Version);

    internal static bool ValidQueueSegment(string segment) => segment.Length is > 0 and <= 260
        && segment is not ("." or "..") && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~');

    private static QueueMetadata Queue(string namespaceId, JsonElement row)
    {
        var id = Required(row, "id");
        var prefix = namespaceId + "/queues/";
        if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InspectionError("invalid-response");
        var segment = id[prefix.Length..];
        if (!ValidQueueSegment(segment))
            throw new InspectionError("invalid-response");
        var props = row.GetProperty("properties");
        long? deadLetters = null;
        if (props.TryGetProperty("countDetails", out var details) && details.ValueKind == JsonValueKind.Object &&
            details.TryGetProperty("deadLetterMessageCount", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt64(out var value)) deadLetters = value;
        return new(prefix + segment, Required(row, "name"), Text(props, "status"), Text(props, "defaultMessageTimeToLive"),
            props.TryGetProperty("maxDeliveryCount", out var max) && max.ValueKind == JsonValueKind.Number && max.TryGetInt32(out var limit) ? limit : null, deadLetters);
    }

    internal sealed record QueueCheck(ServiceBusQueue? Queue, string Visibility, ServiceBusPolicyList? Policies, bool? LocalAuthEnabled);

    private static async Task<bool?> NamespaceLocalAuth(ArmMetadataSession session, string resourceId)
    {
        var ns = await session.Resource(resourceId, row =>
        {
            var props = row.GetProperty("properties");
            return new NamespaceAuth(props.TryGetProperty("disableLocalAuth", out var flag) && flag.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? !flag.GetBoolean() : null);
        });
        return ns.Value?.Enabled;
    }

    internal async Task<QueueCheck> ReadQueueCheck(ServiceBusConnection item, string? queue, bool policy, bool namespaceAuth, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            var session = new ReadSession(http, await GetToken(manager, credentials, item.AuthMode, deadline.Token, item.CredentialProfileId, item.NamespaceResourceId), ct, deadline.Token);
            var localAuth = namespaceAuth ? await NamespaceLocalAuth(session, item.NamespaceResourceId) : null;
            var path = item.NamespaceResourceId;
            if (queue is null)
                return new(null, "unknown", policy ? await session.Policies(path) : null, localAuth);
            var (row, visibility) = await session.KnownQueue(path, queue);
            if (row is null) return new(null, visibility, null, localAuth);
            var policies = policy ? await session.Policies(row.Path) : null;
            return new(new(row.Name, row.Status, policies ?? new("unknown", []), row.Ttl, row.MaxDelivery, row.DeadLetters), visibility, policies, localAuth);
        }
        catch (Exception ex) when (Failure(ex, ct) is { } reason) { return new(null, reason, null, null); }
    }

    public async Task<ServiceBusConnectionTestResult> ValidateAsync(string namespaceId, SecretProviderAuthMode mode, CancellationToken ct, Guid? profileId = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var session = new ReadSession(http, await GetToken(manager, credentials, mode, deadline.Token, profileId, namespaceId), ct, deadline.Token);
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
        bool? localAuthEnabled = null;
        try
        {
            var session = new ReadSession(http, await GetToken(manager, credentials, item.AuthMode, deadline.Token, item.CredentialProfileId, item.NamespaceResourceId), ct, deadline.Token);
            localAuthEnabled = await NamespaceLocalAuth(session, item.NamespaceResourceId);
            var list = await session.List(item.NamespaceResourceId + "/queues", row => Queue(item.NamespaceResourceId, row));
            queueVisibility = list.Status;
            namespacePolicies = await session.Policies(item.NamespaceResourceId);
            var visible = list.Items.DistinctBy(q => q.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(q => q.Path, StringComparer.OrdinalIgnoreCase);
            // A Reader assignment on a single queue can allow a direct read while hiding the namespace listing.
            foreach (var name in expected.Select(q => q.Name).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                var path = item.NamespaceResourceId + "/queues/" + name.Replace('/', '~');
                if (visible.ContainsKey(path)) continue;
                var lookup = await session.KnownQueue(item.NamespaceResourceId, name);
                if (lookup.Queue is { } known) visible.TryAdd(known.Path, known);
                if (queueVisibility == "visible" && lookup.Status == "denied") queueVisibility = "limited";
            }
            foreach (var queue in visible.Values.OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase))
                queues.Add(new(queue.Name, queue.Status, await session.Policies(queue.Path), queue.Ttl, queue.MaxDelivery, queue.DeadLetters));
        }
        catch (Exception e) when (Failure(e, ct) is { } status)
        {
            queueVisibility = status;
            namespacePolicies = new(status, []);
        }
        var names = queues.Select(q => q.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inventory = expected.Select(q => new ServiceBusQueueMatch(q.Id, q.Name, names.Contains(q.Name) || names.Contains(q.Name.Replace('/', '~')) ? "observed" : "unknown")).ToArray();
        return new(item.NamespaceResourceId, observed, queueVisibility, queues, namespacePolicies, inventory, localAuthEnabled);
    }
}
