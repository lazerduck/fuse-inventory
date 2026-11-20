namespace Fuse.Core.Models;

public record CosmosDbConnection(Guid Id, string Name, Guid DataStoreId, string AccountResourceId,
    SecretProviderAuthMode AuthMode, DateTime CreatedAt, DateTime UpdatedAt, Guid? CredentialProfileId = null);
