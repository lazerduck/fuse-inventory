using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateServiceBusIntegration(string Name, Guid MessageBrokerId, string NamespaceResourceId, SecretProviderAuthMode AuthMode, Guid? CredentialProfileId = null);
public record UpdateServiceBusIntegration(Guid Id, string Name, Guid MessageBrokerId, string NamespaceResourceId, SecretProviderAuthMode AuthMode, Guid? CredentialProfileId = null, bool UseSharedCredential = false);
public record DeleteServiceBusIntegration(Guid Id);
public record ValidateServiceBusConnection(string NamespaceResourceId, SecretProviderAuthMode AuthMode, Guid? CredentialProfileId = null);
