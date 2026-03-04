namespace Fuse.Core.Models;

public record BrokerQueue(
    Guid Id,
    string Name,
    string? Description
);

public record BrokerTopic(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string>? Subscribers = null
);

public record MessageBroker
(
    Guid Id,
    string Name,
    string? Description,
    string Kind,
    Guid EnvironmentId,
    Uri? ConnectionUri,
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<BrokerQueue>? Queues = null,
    IReadOnlyList<BrokerTopic>? Topics = null
);
