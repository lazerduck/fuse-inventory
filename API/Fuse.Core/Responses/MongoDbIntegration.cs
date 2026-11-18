namespace Fuse.Core.Responses;
public record MongoDbIntegrationResponse(Guid Id, string Name, Guid DataStoreId, Uri Endpoint,
    string Username, bool HasPassword, DateTime CreatedAt, DateTime UpdatedAt, string AuthenticationDatabase, bool UseTls);
public record MongoDbConnectionTestResult(bool IsSuccessful, bool Authenticated, string Status, string? ErrorMessage);
public record MongoDbRoleReference(string Name, string Database);
public record MongoDbPrivilegeResponse(string Scope, string? Database, string? Collection, IReadOnlyList<string> Actions);
public record MongoDbUserResponse(string Username, string AuthenticationDatabase, IReadOnlyList<MongoDbRoleReference> Roles,
    IReadOnlyList<MongoDbRoleReference> InheritedRoles, IReadOnlyList<MongoDbPrivilegeResponse> Privileges,
    string PrivilegesStatus, IReadOnlyList<Guid> MatchedAccountIds);
public record MongoDbRoleResponse(string Name, string Database, bool IsBuiltin, IReadOnlyList<MongoDbRoleReference> Roles,
    IReadOnlyList<MongoDbRoleReference> InheritedRoles, IReadOnlyList<MongoDbPrivilegeResponse> Privileges);
public record MongoDbUserScopeResponse(string AuthenticationDatabase, string Status);
public record MongoDbAccountMatchResponse(Guid AccountId, string? Username, string? AuthenticationDatabase, string Status);
public record MongoDbDiscoveryResponse(Guid IntegrationId, DateTime ObservedAt, string Database, string ConnectionStatus,
    string CollectionsStatus, IReadOnlyList<string> Collections, string RolesStatus, IReadOnlyList<MongoDbRoleResponse> Roles,
    IReadOnlyList<MongoDbUserScopeResponse> UserScopes, IReadOnlyList<MongoDbUserResponse> Users,
    IReadOnlyList<MongoDbAccountMatchResponse> Accounts, IReadOnlyList<string> Warnings);
