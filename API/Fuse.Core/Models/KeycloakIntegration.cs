namespace Fuse.Core.Models;

public record KeycloakIntegration(Guid Id, string Name, Uri Issuer, string ClientId, string ProtectedClientSecret,
    DateTime CreatedAt, DateTime UpdatedAt);
