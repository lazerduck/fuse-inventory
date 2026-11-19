namespace Fuse.Core.Commands;

public record CreateKeycloakIntegration(string Name, Uri Issuer, string ClientId, string ClientSecret);
public record UpdateKeycloakIntegration(Guid Id, string Name, Uri Issuer, string ClientId, string? ClientSecret);
public record DeleteKeycloakIntegration(Guid Id);
public record ValidateKeycloakConnection(Uri Issuer, string ClientId, string ClientSecret);
