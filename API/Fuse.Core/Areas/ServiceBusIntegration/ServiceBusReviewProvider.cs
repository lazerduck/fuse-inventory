using System.Globalization;
using System.Xml;
using System.Text.Json;
using Fuse.Core.Areas.AzureMetadata;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Models;
using static Fuse.Core.Areas.AzureMetadata.AzureReview;

namespace Fuse.Core.Areas.ServiceBusIntegration;

public sealed class ServiceBusReviewProvider(ServiceBusInspector inspector) : IIntegrationReviewProvider
{
    public string Provider => "servicebus";
    public IReadOnlyList<ReviewCheck> Checks { get; } =
    [
        new("queue-settings", "Queue settings and dead letters", [new("queue", "Queue"),
            new("ttl", "Default message TTL", false, Hint: "ISO duration, for example P14D"), new("maxDelivery", "Maximum deliveries", false),
            new("maxDeadLetters", "Maximum dead letters", false)]),
        new("sas-policy", "SAS policy rights", [new("scope", "Policy scope", Choices: ["queue", "namespace"]),
            new("queue", "Queue (queue scope)", false), new("policy", "Policy"), new("rights", "Expected rights", Hint: "Comma-separated Send, Listen or Manage"),
            new("localAuth", "Expected SAS authentication", false, ["enabled", "disabled"])]),
        new("data-role", "Entra queue data-role assignment", [new("queue", "Queue"), new("identityId", "Fuse identity"),
            new("role", "Built-in data role", Choices: ["Sender", "Receiver", "Owner"])]),
        new("subscription-filter", "Topic subscription SQL filter", [new("topic", "Topic"), new("subscription", "Subscription"), new("rule", "Rule"),
            new("filter", "Expected SQL expression")])
    ];

    public string? Validate(Snapshot snapshot, ReviewInput input)
    {
        var item = snapshot.ServiceBusIntegrations.FirstOrDefault(s => s.Id == input.IntegrationId);
        if (item is null || !ServiceBusInspector.Valid(item.NamespaceResourceId, item.AuthMode)) return "Select a Service Bus integration.";
        if (input.AccountId is not null) return "Service Bus checks use a broker entity or a bound Fuse identity.";
        if (input.Kind == "subscription-filter")
            return new[] { "topic", "subscription", "rule" }.All(k => Field(input, k) is { } v &&
                (ServiceBusInspector.ValidQueueSegment(v) || k == "rule" && v == "$Default")) ? null : "Specify valid topic, subscription and rule names.";
        if ((input.Kind != "sas-policy" || Field(input, "scope") == "queue") &&
            (Field(input, "queue") is not { } queue || !ServiceBusInspector.ValidQueueSegment(queue.Replace('/', '~')))) return "Specify a valid queue.";
        if (input.Kind == "data-role") return AzureRoleReview.Identity(snapshot, input) is null ? "Select a Fuse identity with an Azure principal binding." : null;
        if (input.Kind == "sas-policy")
        {
            var rights = Rights(Field(input, "rights") ?? "");
            return Field(input, "policy") is not null && rights.Length > 0 && rights.All(r => r is "Send" or "Listen" or "Manage")
                ? null : "Specify a policy and its expected rights.";
        }
        if (input.Kind != "queue-settings") return "Unknown Service Bus check.";
        var ttl = Field(input, "ttl");
        if (ttl is null && Field(input, "maxDelivery") is null && Field(input, "maxDeadLetters") is null)
            return "Specify at least one queue expectation.";
        if (!Integer(Field(input, "maxDelivery"), 1) || !Integer(Field(input, "maxDeadLetters"), 0))
            return "Enter positive maximum deliveries and a nonnegative dead-letter limit.";
        try { if (ttl is not null && XmlConvert.ToTimeSpan(ttl) <= TimeSpan.Zero) return "Enter a positive ISO duration."; }
        catch (Exception e) when (e is FormatException or OverflowException) { return "Enter a valid ISO duration."; }
        return null;
    }

    private static string[] Rights(string rights) => rights.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    public async Task<ReviewEvidence> InspectAsync(Snapshot snapshot, ReviewInput input, CancellationToken ct)
    {
        var item = snapshot.ServiceBusIntegrations.Single(s => s.Id == input.IntegrationId);
        var queue = Field(input, "queue");
        var result = new AzureReview();
        if (input.Kind is "data-role" or "subscription-filter")
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            var session = await inspector.Open(item, ct, deadline.Token);
            if (input.Kind == "data-role") return await AzureRoleReview.Bus(session, item.NamespaceResourceId + "/queues/" + queue!.Replace('/', '~'),
                AzureRoleReview.Identity(snapshot, input)!, input.Fields["role"]);
            var entity = "/topics/" + input.Fields["topic"] + "/subscriptions/" + input.Fields["subscription"] + "/rules/" + Uri.EscapeDataString(input.Fields["rule"]);
            var rule = await session.Resource(item.NamespaceResourceId + entity, r =>
            {
                var props = r.GetProperty("properties");
                return props.TryGetProperty("sqlFilter", out var filter) && filter.ValueKind == JsonValueKind.Object
                    ? ArmMetadataSession.Text(filter, "sqlExpression") ?? "(no expression)" : "(no SQL filter)";
            });
            if (rule.Value is null) result.Unknown("Subscription rule: " + rule.Status);
            else result.Compare("SQL expression", input.Fields["filter"], rule.Value);
            return result.Result(input.Fields["topic"] + "/" + input.Fields["subscription"] + "/" + input.Fields["rule"]);
        }
        var namespacePolicy = input.Kind == "sas-policy" && input.Fields["scope"] == "namespace";
        var discovery = await inspector.ReadQueueCheck(item, namespacePolicy ? null : queue, input.Kind == "sas-policy", Field(input, "localAuth") is not null, ct);
        var observed = discovery.Queue;
        if (input.Kind == "sas-policy")
        {
            var policies = discovery.Policies;
            var policy = policies?.Policies.FirstOrDefault(p => p.Name.Equals(input.Fields["policy"], StringComparison.OrdinalIgnoreCase));
            if (policy is null) result.Unknown("The named policy was not observed. Policy visibility: " + (policies?.Visibility ?? "unknown"));
            else result.Compare("SAS rights", string.Join(", ", Rights(input.Fields["rights"])), string.Join(", ", policy.Rights.Order(StringComparer.Ordinal)));
            if (Field(input, "localAuth") is { } auth) result.Compare("Namespace SAS authentication", auth,
                discovery.LocalAuthEnabled is { } enabled ? enabled ? "enabled" : "disabled" : null);
            return result.Result(input.Fields["policy"]);
        }
        if (observed is null) result.Unknown("Queue not visible: " + discovery.Visibility);
        else
        {
            if (Field(input, "ttl") is { } ttl)
                result.Compare("Default message TTL", XmlConvert.ToTimeSpan(ttl).ToString(), observed.DefaultMessageTimeToLive is { } value ? XmlConvert.ToTimeSpan(value).ToString() : null);
            if (Field(input, "maxDelivery") is { } max)
                result.Compare("Maximum deliveries", int.Parse(max, CultureInfo.InvariantCulture).ToString(), observed.MaxDeliveryCount?.ToString());
            if (Field(input, "maxDeadLetters") is { } threshold)
            {
                if (observed.DeadLetterMessageCount is not { } count) result.Unknown("Dead-letter count was not returned.");
                else
                {
                    int limit = int.Parse(threshold, CultureInfo.InvariantCulture);
                    result.Compare("Dead-letter count", $"≤ {limit}", count <= limit ? $"≤ {limit}" : count.ToString());
                    return result.Result($"{queue} · {count} dead letters observed");
                }
            }
        }
        return result.Result(queue!);
    }
}
