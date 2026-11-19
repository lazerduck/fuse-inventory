using System.Text.RegularExpressions;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.RabbitMqIntegration;

public sealed class RabbitMqReviewProvider(IRabbitMqInspector inspector) : IIntegrationReviewProvider
{
    private static readonly ReviewField Queue = new("queue", "Queue");
    private static readonly ReviewField Exchange = new("exchange", "Publisher exchange", false, Hint: "Leave blank for the default exchange.");
    private static readonly ReviewField Key = new("routingKey", "Message routing key", false);
    private static readonly ReviewField Permission = new("expected", "Expected authorization", Choices: ["allowed", "denied"]);
    public string Provider => "rabbitmq";
    public IReadOnlyList<ReviewCheck> Checks { get; } = [
        new("route", "Message route", [Exchange, Key, Queue]),
        new("publish", "Publisher permission", [Exchange, Key, Permission], RequiresAccount: true),
        new("consume", "Consumer permission", [Queue, Permission], RequiresAccount: true),
        new("queue", "Queue configuration", [Queue, new("type", "Queue type", Choices: ["classic", "quorum", "stream"]),
            new("durable", "Durable", Choices: ["true", "false"])]),
        new("dead-letter", "Dead-letter route", [Queue, new("destination", "Expected dead-letter queue"),
            new("routingKey", "Original routing key", false, Hint: "Used if the source queue has no dead-letter routing key.")])
    ];

    public string? Validate(Snapshot snapshot, ReviewInput input)
    {
        var connection = snapshot.RabbitMqIntegrations.FirstOrDefault(i => i.Id == input.IntegrationId);
        if (connection == null) return "Select a RabbitMQ integration.";
        if (input.Fields.Values.Any(v => System.Text.Encoding.UTF8.GetByteCount(v) > 255 || v.Any(char.IsControl)))
            return "RabbitMQ names and routing keys must be at most 255 bytes without control characters.";
        if (input.AccountId != null && !snapshot.Accounts.Any(a => a.Id == input.AccountId && a.TargetKind == TargetKind.MessageBroker
            && a.TargetId == connection.MessageBrokerId && !string.IsNullOrWhiteSpace(a.UserName)))
            return "Select an account with a username on this RabbitMQ broker.";
        return null;
    }

    public async Task<ReviewEvidence> InspectAsync(Snapshot snapshot, ReviewInput input, CancellationToken ct)
    {
        var connection = snapshot.RabbitMqIntegrations.Single(i => i.Id == input.IntegrationId);
        var discovery = await inspector.DiscoverAsync(connection, [], [], ct);
        var fields = input.Fields;
        var account = snapshot.Accounts.FirstOrDefault(a => a.Id == input.AccountId);
        var context = $"Virtual host {connection.VirtualHost}";
        var queueName = fields.GetValueOrDefault("queue", "");
        var queue = discovery.Resources.FirstOrDefault(r => r.Kind == "queues" && r.Name == queueName);
        var exchange = fields.GetValueOrDefault("exchange", "");
        var key = fields.GetValueOrDefault("routingKey", "");
        if (input.Kind is "publish" or "consume")
            return Access(discovery, account?.UserName, input.Kind == "publish", exchange, key, queueName, fields["expected"], context);
        if (discovery.TopologyStatus != "complete")
            return new(ReviewStatus.Unknown, "Virtual-host topology is incomplete.", [context, discovery.TopologyStatus]);
        if (queue == null) return new(ReviewStatus.Drift, $"Queue {queueName} is missing.", [context]);
        if (input.Kind == "queue")
        {
            if (queue.Durable == null || string.IsNullOrEmpty(queue.Type))
                return new(ReviewStatus.Unknown, "Queue configuration is incomplete.", [context, queueName]);
            var matches = queue.Type == fields["type"] && queue.Durable.Value == (fields["durable"] == "true");
            return new(matches ? ReviewStatus.Matched : ReviewStatus.Drift, matches ? "Queue configuration matches." : "Queue configuration differs.",
                [context, $"{queueName}: type {queue.Type}, durable {queue.Durable.Value.ToString().ToLowerInvariant()}"]);
        }
        if (input.Kind == "dead-letter")
        {
            if (!queue.RoutingOptionsKnown)
                return new(ReviewStatus.Unknown, "The queue's effective dead-letter settings are unavailable.", [context]);
            if (queue.DeadLetterExchange == null)
                return new(ReviewStatus.Drift, $"Queue {queueName} has no configured dead-letter exchange.", [context]);
            exchange = queue.DeadLetterExchange;
            key = queue.DeadLetterRoutingKey ?? key;
            context += $" · dead letters from {queueName}";
            queueName = fields["destination"];
        }
        return Route(discovery.Resources, exchange, key, queueName, context);
    }

    // Follow exchange-to-exchange bindings inside this vhost. Unknown exchange semantics
    // or alternate-exchange fallback must not turn limited evidence into a broken route.
    internal static ReviewEvidence Route(IReadOnlyList<RabbitMqResource> resources, string source, string key, string queue, string context)
    {
        var exchanges = resources.Where(r => r.Kind == "exchanges").ToDictionary(r => r.Name, StringComparer.Ordinal);
        var queues = resources.Where(r => r.Kind == "queues").Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        var bindings = resources.Where(r => r.Kind == "bindings").ToLookup(r => r.Name, StringComparer.Ordinal);
        if (!queues.Contains(queue)) return new(ReviewStatus.Drift, $"Destination queue {queue} is missing.", [context]);
        if (source.Length > 0 && !exchanges.ContainsKey(source)) return new(ReviewStatus.Drift, $"Exchange {source} is missing.", [context]);
        var paths = new Dictionary<string, RabbitMqResource?> { [source] = null };
        var explored = new List<string>();
        var pending = new Queue<string>();
        pending.Enqueue(source);
        var unresolved = false;
        while (pending.TryDequeue(out var current))
        {
            if (current == "")
            {
                if (key == queue) return Found(current, key);
                explored.Add($"The default exchange selects a queue named '{key}'.");
                continue;
            }
            if (!exchanges.TryGetValue(current, out var exchange)) { unresolved = true; continue; }
            var outgoing = bindings[current];
            if (explored.Count < 12)
            {
                var examples = outgoing.Take(4).Select(b => $"'{b.RoutingKey}' → {b.Destination}");
                explored.Add($"{exchange.Type} exchange {current}: {outgoing.Count()} bindings ({string.Join(", ", examples)}).");
            }
            foreach (var binding in outgoing)
            {
                bool? matches = exchange.Type switch
                {
                    "fanout" => true,
                    "direct" => binding.RoutingKey == key,
                    "topic" => TopicMatches(binding.RoutingKey ?? "", key),
                    _ => null
                };
                if (matches is null) { unresolved = true; continue; }
                if (matches == false || binding.Destination is null) continue;
                if (binding.Type == "queue" && binding.Destination == queue) return Found(current, binding.RoutingKey ?? "");
                if (binding.Type == "exchange" && paths.TryAdd(binding.Destination, binding)) pending.Enqueue(binding.Destination);
            }
            if (exchange.AlternateExchange != null || !exchange.RoutingOptionsKnown) unresolved = true;
        }
        return new(unresolved ? ReviewStatus.Unknown : ReviewStatus.Drift,
            unresolved ? "The complete route cannot be established." : $"No matching route to {queue}.",
            [context, $"Exchange {Display(source)}, routing key '{key}'", .. explored]);

        ReviewEvidence Found(string lastExchange, string bindingKey)
        {
            var chain = new Stack<string>();
            var steps = new Stack<string>();
            var at = lastExchange;
            steps.Push(Binding(at, bindingKey, queue));
            chain.Push(Display(at));
            while (paths[at] is { } link)
            {
                steps.Push(Binding(link.Name, link.RoutingKey ?? "", at));
                at = link.Name;
                chain.Push(Display(at));
            }
            return new(ReviewStatus.Matched, $"Configured route reaches {queue}.",
                [context, $"{string.Join(" → ", chain)} → {queue}", $"Message key '{key}'", .. steps.Take(16)]);
        }
        string Binding(string name, string pattern, string destination) =>
            $"{(name.Length == 0 ? "direct" : exchanges[name].Type)} exchange {Display(name)}: binding '{pattern}' → {destination}";
    }

    private static string Display(string exchange) => exchange == "" ? "(default exchange)" : exchange;

    // A topic '*' consumes one word and '#' consumes zero or more. Dynamic programming
    // bounds work for repeated '#' and keys at the protocol's 255-byte limit.
    internal static bool TopicMatches(string binding, string key)
    {
        var words = key.Length == 0 ? [] : key.Split('.');
        var reachable = new bool[words.Length + 1];
        reachable[0] = true;
        foreach (var part in binding.Length == 0 ? Array.Empty<string>() : binding.Split('.'))
        {
            var next = new bool[reachable.Length];
            for (var i = 0; i < next.Length; i++)
                next[i] = part == "#" ? reachable[i] || i > 0 && next[i - 1]
                    : i > 0 && reachable[i - 1] && (part == "*" || part == words[i - 1]);
            reachable = next;
        }
        return reachable[^1];
    }

    private static ReviewEvidence Access(RabbitMqDiscoveryResponse discovery, string? user, bool publish, string exchange,
        string key, string queue, string expected, string context)
    {
        var details = new List<string> { context, $"User {user}: configured internal RabbitMQ permissions" };
        if (user == null || discovery.PermissionsStatus != "complete")
            return new(ReviewStatus.Unknown, "Resource permissions are unavailable.", details);
        var permissions = discovery.Permissions.FirstOrDefault(p => p.Username == user);
        if (permissions == null && !discovery.Users.Contains(user, StringComparer.Ordinal))
            return new(ReviewStatus.Unknown, "No internal authorization record was observed for this account.", details);
        var resource = publish ? exchange == "" ? "amq.default" : exchange : queue;
        var expression = publish ? permissions?.Write : permissions?.Read;
        bool? allowed = expression == null ? false : MatchPermission(expression, resource);
        details.Add($"{(publish ? "Write" : "Read")} '{resource}' · {expression ?? "(no resource grant)"}");
        if (publish && allowed == true && exchange != "")
        {
            var source = discovery.Resources.FirstOrDefault(r => r.Kind == "exchanges" && r.Name == exchange);
            if (source == null || string.IsNullOrEmpty(source.Type)) allowed = null;
            else if (source.Type == "topic")
            {
                if (discovery.TopicPermissionsStatus != "complete") allowed = null;
                else
                {
                    var topic = discovery.TopicPermissions.FirstOrDefault(p => p.Exchange == exchange && p.Username == user);
                    details.Add($"Topic key '{key}' · {topic?.Write ?? "(no additional topic restriction)"}");
                    if (topic != null) allowed = MatchPermission(topic.Write, key);
                }
            }
        }
        if (allowed == null) return new(ReviewStatus.Unknown, "Configured authorization could not be established.", details);
        var actual = allowed.Value ? "allowed" : "denied";
        return new(actual == expected ? ReviewStatus.Matched : ReviewStatus.Drift, $"Configured { (publish ? "publish" : "consume") } permission is {actual}.", details);
    }

    // RabbitMQ uses Erlang PCRE and can expand authorization variables. A portable
    // literal/wildcard subset avoids guessing at PCRE-only or per-connection rules.
    internal static bool? MatchPermission(string rule, string name)
    {
        if (rule.Length == 0) return false;
        if (rule.Length > 512 || rule.Any(c => !char.IsAsciiLetterOrDigit(c) && !@"._-^$*+?|[]() \".Contains(c))) return null;
        if (rule.Contains("(?") || Regex.IsMatch(rule, @"\\[A-Za-z0-9]")) return null;
        try { return Regex.IsMatch(name, rule, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(50)); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or RegexMatchTimeoutException) { return null; }
    }
}
