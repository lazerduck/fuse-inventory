namespace Fuse.Core.Commands;

public record CreateRedisIntegration(string Name, Guid DataStoreId, Uri Endpoint, string? Username, string? Password);
/// <summary>A null password retains the saved password; an empty password explicitly clears it.</summary>
public record UpdateRedisIntegration(Guid Id, string Name, Guid DataStoreId, Uri Endpoint, string? Username, string? Password);
public record DeleteRedisIntegration(Guid Id);
public record ValidateRedisConnection(Uri Endpoint, string? Username, string? Password);
