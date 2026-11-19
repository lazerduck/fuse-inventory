namespace Fuse.Core.Models;

public record ServiceBusConnection(Guid Id, string Name, Guid MessageBrokerId, string NamespaceResourceId,
    SecretProviderAuthMode AuthMode, DateTime CreatedAt, DateTime UpdatedAt);
