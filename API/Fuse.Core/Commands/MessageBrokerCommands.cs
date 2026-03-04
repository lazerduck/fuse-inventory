namespace Fuse.Core.Commands;

public record CreateMessageBroker(
    string Name,
    string? Description,
    string Kind,
    Guid EnvironmentId,
    Uri? ConnectionUri,
    HashSet<Guid>? TagIds = null
);

public record UpdateMessageBroker(
    Guid Id,
    string Name,
    string? Description,
    string Kind,
    Guid EnvironmentId,
    Uri? ConnectionUri,
    HashSet<Guid>? TagIds = null
);

public record DeleteMessageBroker(
    Guid Id
);
