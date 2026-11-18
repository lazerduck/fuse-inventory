namespace Fuse.Core.Models;

/// <summary>Private connection configuration. Public API responses must use RedisIntegrationResponse.</summary>
public record RedisIntegration(Guid Id, string Name, Guid DataStoreId, Uri Endpoint,
    string Username, string? Password, DateTime CreatedAt, DateTime UpdatedAt);
