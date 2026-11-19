namespace Fuse.Core.Responses;

public record KeycloakIntegrationResponse(Guid Id, string Name, Uri Issuer, string ClientId, DateTime CreatedAt, DateTime UpdatedAt);
public record KeycloakConnectionTestResult(bool IsSuccessful, string Status);
public record KeycloakRole(string? ClientId, string Name);
public record KeycloakClient(string Id, string ClientId, bool? Enabled, bool? ServiceAccountsEnabled,
    string? ServiceAccount, string RolesStatus, IReadOnlyList<KeycloakRole> DirectRoles, IReadOnlyList<KeycloakRole> EffectiveRoles);
public record KeycloakAccountMatch(Guid AccountId, string? ClientId, Guid? ApplicationId, string? ApplicationName, string Status);
public record KeycloakDiscoveryResponse(Uri Issuer, DateTimeOffset ObservedAt, string ConnectionStatus, string ClientsStatus,
    IReadOnlyList<KeycloakClient> Clients, IReadOnlyList<KeycloakAccountMatch> Accounts);
