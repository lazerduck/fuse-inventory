namespace Fuse.Core.Models;

/// <summary>Private connection configuration. Public API responses must use MongoDbIntegrationResponse.</summary>
public record MongoDbIntegration(Guid Id, string Name, Guid DataStoreId, Uri Endpoint,
    string Username, string? Password, DateTime CreatedAt, DateTime UpdatedAt, string AuthenticationDatabase, bool UseTls);
