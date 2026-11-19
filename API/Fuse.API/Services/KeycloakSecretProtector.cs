using Fuse.Core.Areas.KeycloakIntegration;
using Microsoft.AspNetCore.DataProtection;

namespace Fuse.API.Services;

public sealed class KeycloakSecretProtector(IDataProtectionProvider provider) : IKeycloakSecretProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("Fuse.Keycloak.InspectionClientSecret.v1");
    public string Protect(string secret) => protector.Protect(secret);
    public string Unprotect(string protectedSecret) => protector.Unprotect(protectedSecret);
}
