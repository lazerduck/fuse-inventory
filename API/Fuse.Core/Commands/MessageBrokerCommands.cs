namespace Fuse.Core.Commands;

public record BrokerQueueInput(
    string Name,
    string? Description
);

public record BrokerTopicInput(
    string Name,
    string? Description,
    IReadOnlyList<string>? Subscribers = null
);

public record CreateMessageBroker(
    string Name,
    string? Description,
    string Kind,
    Guid EnvironmentId,
    Uri? ConnectionUri,
    IReadOnlyList<BrokerQueueInput>? Queues = null,
    IReadOnlyList<BrokerTopicInput>? Topics = null,
    HashSet<Guid>? TagIds = null
);

public record UpdateMessageBroker(
    Guid Id,
    string Name,
    string? Description,
    string Kind,
    Guid EnvironmentId,
    Uri? ConnectionUri,
    IReadOnlyList<BrokerQueueInput>? Queues = null,
    IReadOnlyList<BrokerTopicInput>? Topics = null,
    HashSet<Guid>? TagIds = null
);

public record DeleteMessageBroker(
    Guid Id
);
