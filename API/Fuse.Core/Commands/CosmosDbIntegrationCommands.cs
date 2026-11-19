using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateCosmosDbIntegration(string Name, Guid DataStoreId, string AccountResourceId, SecretProviderAuthMode AuthMode, Guid? CredentialProfileId = null);
public record UpdateCosmosDbIntegration(Guid Id, string Name, Guid DataStoreId, string AccountResourceId, SecretProviderAuthMode AuthMode, Guid? CredentialProfileId = null, bool UseSharedCredential = false);
public record DeleteCosmosDbIntegration(Guid Id);
public record ValidateCosmosDbConnection(string AccountResourceId, SecretProviderAuthMode AuthMode, Guid? CredentialProfileId = null);
