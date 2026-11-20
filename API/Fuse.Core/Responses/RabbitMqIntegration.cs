namespace Fuse.Core.Responses;

public record RabbitMqIntegrationResponse(Guid Id, string Name, Guid MessageBrokerId, string VirtualHost, Uri Endpoint, string Username, bool HasPassword, DateTime CreatedAt, DateTime UpdatedAt);
public record RabbitMqConnectionTestResult(bool IsSuccessful, bool Authenticated, string Status, string? ErrorMessage);
public record RabbitMqResource(string Name, string Kind, string VirtualHost, string? Type = null, string? Destination = null, string? RoutingKey = null,
    bool? Durable = null, string? DeadLetterExchange = null, string? DeadLetterRoutingKey = null,
    bool RoutingOptionsKnown = false, string? AlternateExchange = null);
public record RabbitMqQueueMatch(string Name, string Status);
public record RabbitMqPermission(string Username, string VirtualHost, string Configure, string Write, string Read);
public record RabbitMqTopicPermission(string Username, string VirtualHost, string Exchange, string Write, string Read);
public record RabbitMqAccountMatch(Guid AccountId, string? Username, string Status);
public record RabbitMqDiscoveryResponse(Guid IntegrationId, DateTime ObservedAt, string VirtualHost, string ConnectionStatus, string TopologyStatus, string UsersStatus, string PermissionsStatus, IReadOnlyList<RabbitMqResource> Resources, IReadOnlyList<RabbitMqQueueMatch> Queues, IReadOnlyList<string> Users, IReadOnlyList<RabbitMqPermission> Permissions, IReadOnlyList<RabbitMqAccountMatch> Accounts, IReadOnlyList<string> Warnings)
{
    public string TopicPermissionsStatus { get; init; } = "unknown";
    public IReadOnlyList<RabbitMqTopicPermission> TopicPermissions { get; init; } = [];
}
