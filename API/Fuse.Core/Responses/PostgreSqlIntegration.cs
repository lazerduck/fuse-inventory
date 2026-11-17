namespace Fuse.Core.Responses;
public record PostgreSqlIntegrationResponse(Guid Id, string Name, Guid DataStoreId, Uri Endpoint,
    string Username, bool HasPassword, DateTime CreatedAt, DateTime UpdatedAt);
public record PostgreSqlConnectionTestResult(bool IsSuccessful, bool Authenticated, string Status, string? ErrorMessage);
public record PostgreSqlRoleResponse(string Name, bool CanLogin, bool Inherit, bool Superuser, bool CreateRole,
    bool CreateDatabase, bool BypassRowSecurity, bool CanConnect, IReadOnlyList<Guid> MatchedAccountIds);
public record PostgreSqlMembershipResponse(string Member, string Role, bool Direct, bool Inherited, bool CanSetRole);
public record PostgreSqlPrivilegeResponse(string Role, string Kind, string Name, IReadOnlyList<string> Privileges);
public record PostgreSqlAccountMatchResponse(Guid AccountId, string? Username, string Status);
public record PostgreSqlDiscoveryResponse(Guid IntegrationId, DateTime ObservedAt, string Database,
    string ConnectionStatus, string RolesStatus, string MembershipsStatus, string PermissionsStatus,
    IReadOnlyList<PostgreSqlRoleResponse> Roles, IReadOnlyList<PostgreSqlMembershipResponse> Memberships,
    IReadOnlyList<PostgreSqlPrivilegeResponse> Permissions, IReadOnlyList<PostgreSqlAccountMatchResponse> Accounts,
    IReadOnlyList<string> Warnings);
