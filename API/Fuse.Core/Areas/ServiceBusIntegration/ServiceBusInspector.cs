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

    private sealed record QueueMetadata(string Path, string Name, string? Status);

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
            var session = new ReadSession(http, await GetToken(manager, credentials, mode, deadline.Token), ct, deadline.Token);
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
            var session = new ReadSession(http, await GetToken(manager, credentials, item.AuthMode, deadline.Token), ct, deadline.Token);
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
