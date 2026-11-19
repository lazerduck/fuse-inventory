using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Areas.RabbitMqIntegration;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class RabbitMqReviewTests
{
    private static IEnumerable<RabbitMqResource> Graph(string kind, string binding, bool alternate = false)
    {
        yield return new("events", "exchanges", "lab", kind, RoutingOptionsKnown: true, AlternateExchange: alternate ? "fallback" : null);
        yield return new("second", "exchanges", "lab", "fanout", RoutingOptionsKnown: true);
        yield return new("orders", "queues", "lab", "quorum", Durable: true, RoutingOptionsKnown: true);
        yield return new("events", "bindings", "lab", "exchange", "second", binding);
        yield return new("second", "bindings", "lab", "exchange", "events", "");
        yield return new("second", "bindings", "lab", "queue", "orders", "");
    }

    private static async Task<ReviewEvidence> Review(string kind, Dictionary<string, string> fields,
        IEnumerable<RabbitMqResource> graph, string topics = "complete", string? resourceRule = ".*", string topicRule = "^order",
        string topology = "complete")
    {
        var store = new InMemoryFuseStore();
        var item = new RabbitMqIntegration(Guid.NewGuid(), "lab", Guid.NewGuid(), "lab", new Uri("http://rabbit.test"), "observer", null,
            DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(Guid.NewGuid(), item.MessageBrokerId, TargetKind.MessageBroker, AuthKind.UserPassword,
            new(SecretBindingKind.None, null, null), "worker", null, [], [], DateTime.UtcNow, DateTime.UtcNow);
        await store.UpdateAsync(s => s with { RabbitMqIntegrations = [item], Accounts = [account] });
        var data = new RabbitMqDiscoveryResponse(item.Id, DateTime.UtcNow, item.VirtualHost, "connected", topology, "complete",
            resourceRule == null ? "denied" : "complete", graph.ToArray(), [], ["worker"],
            resourceRule == null ? [] : [new("worker", "lab", "", resourceRule, resourceRule)], [], [])
            { TopicPermissionsStatus = topics, TopicPermissions = [new("worker", "lab", "events", topicRule, "")] };
        var inspector = new Mock<IRabbitMqInspector>();
        inspector.Setup(i => i.DiscoverAsync(item, It.IsAny<IReadOnlyList<Account>>(), It.IsAny<IReadOnlyList<BrokerQueue>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(data);
        var provider = new RabbitMqReviewProvider(inspector.Object);
        var input = new ReviewInput("rabbitmq", item.Id, kind, account.Id, fields);
        var snapshot = await store.GetAsync();
        Assert.Null(provider.Validate(snapshot, input));
        return await provider.InspectAsync(snapshot, input, default);
    }

    [Theory]
    [InlineData("topic", "#.created", "created", ReviewStatus.Matched)]
    [InlineData("topic", "order.*", "order.a.b", ReviewStatus.Drift)]
    [InlineData("topic", "order.#", "order", ReviewStatus.Matched)]
    [InlineData("direct", "order.*", "order.a", ReviewStatus.Drift)]
    [InlineData("fanout", "", "anything", ReviewStatus.Matched)]
    [InlineData("headers", "", "order", ReviewStatus.Unknown)]
    public async Task RouteFollowsConfiguredBindingsAndTerminatesOnCycles(string type, string binding, string key, ReviewStatus expected)
    {
        var result = await Review("route", new() { ["exchange"] = "events", ["queue"] = "orders", ["routingKey"] = key }, Graph(type, binding));
        Assert.Equal(expected, result.Status);
        if (expected == ReviewStatus.Matched) Assert.Contains("events → second → orders", result.Details);
    }

    [Theory]
    [InlineData("complete", ".*", "billing", "denied", ReviewStatus.Matched)]
    [InlineData("complete", ".*", "order.created", "denied", ReviewStatus.Drift)]
    [InlineData("denied", ".*", "order.created", "allowed", ReviewStatus.Unknown)]
    [InlineData("denied", "^$", "order.created", "denied", ReviewStatus.Matched)]
    [InlineData("complete", "{username}", "order.created", "allowed", ReviewStatus.Unknown)]
    public async Task PublisherCombinesResourceAndTopicEvidence(string coverage, string rule, string key, string expected, ReviewStatus result)
    {
        Assert.Equal(result, (await Review("publish", new() { ["exchange"] = "events", ["routingKey"] = key, ["expected"] = expected },
            Graph("topic", "#"), coverage, rule)).Status);
    }

    [Fact]
    public async Task DeadLetterRoutingUsesOverrideAndScopeCoverage()
    {
        var resources = Graph("direct", "retry").Append(new("source", "queues", "lab", "quorum", Durable: true,
            DeadLetterExchange: "events", DeadLetterRoutingKey: "retry", RoutingOptionsKnown: true)).ToArray();
        var fields = new Dictionary<string, string> { ["queue"] = "source", ["destination"] = "orders", ["routingKey"] = "original" };
        Assert.Equal(ReviewStatus.Matched, (await Review("dead-letter", fields, resources)).Status);
        Assert.Equal(ReviewStatus.Unknown, (await Review("dead-letter", fields, resources, topology: "partial")).Status);
        Assert.Equal(ReviewStatus.Drift, (await Review("dead-letter", fields, resources.Where(r => r.Name != "events"))).Status);
    }
}
