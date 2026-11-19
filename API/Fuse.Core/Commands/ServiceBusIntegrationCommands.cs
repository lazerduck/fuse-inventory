using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateServiceBusIntegration(string Name, Guid MessageBrokerId, string NamespaceResourceId, SecretProviderAuthMode AuthMode);
public record UpdateServiceBusIntegration(Guid Id, string Name, Guid MessageBrokerId, string NamespaceResourceId, SecretProviderAuthMode AuthMode);
public record DeleteServiceBusIntegration(Guid Id);
public record ValidateServiceBusConnection(string NamespaceResourceId, SecretProviderAuthMode AuthMode);
