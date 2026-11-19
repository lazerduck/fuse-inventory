using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateCosmosDbIntegration(string Name, Guid DataStoreId, string AccountResourceId, SecretProviderAuthMode AuthMode);
public record UpdateCosmosDbIntegration(Guid Id, string Name, Guid DataStoreId, string AccountResourceId, SecretProviderAuthMode AuthMode);
public record DeleteCosmosDbIntegration(Guid Id);
public record ValidateCosmosDbConnection(string AccountResourceId, SecretProviderAuthMode AuthMode);
