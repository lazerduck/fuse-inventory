namespace Fuse.Core.Responses;

public record RedisIntegrationResponse(Guid Id, string Name, Guid DataStoreId, Uri Endpoint,
    string Username, bool HasPassword, DateTime CreatedAt, DateTime UpdatedAt);

public record RedisConnectionTestResult(bool IsSuccessful, bool Authenticated, string Status, string? ErrorMessage);

public record RedisKeyspaceInfo(int Database, long Keys, long Expires);
public record RedisServerInfo(string? Version, string? Mode, string? Role, long? UptimeSeconds,
    long? UsedMemoryBytes, long? ConnectedClients, IReadOnlyList<RedisKeyspaceInfo> Keyspaces);

public record RedisAclSelectorResponse(string? Commands, string? KeyPatterns, string? ChannelPatterns);
/// <summary>Only allowlisted ACL metadata. Passwords and password hashes are never projected.</summary>
public record RedisAclUserResponse(string Username, bool? Enabled, IReadOnlyList<string> Flags,
    string? Commands, string? KeyPatterns, string? ChannelPatterns,
    IReadOnlyList<RedisAclSelectorResponse> Selectors, IReadOnlyList<Guid> MatchedAccountIds, string Status);
public record RedisAccountMatchResponse(Guid AccountId, string? Username, string Status, string? RedisUsername);

public record RedisDiscoveryResponse(Guid IntegrationId, DateTime ObservedAt, string ConnectionStatus,
    string ServerStatus, string AclStatus, bool AclComplete, RedisServerInfo? Server,
    IReadOnlyList<RedisAclUserResponse> Users, IReadOnlyList<RedisAccountMatchResponse> Accounts,
    IReadOnlyList<string> Warnings);
