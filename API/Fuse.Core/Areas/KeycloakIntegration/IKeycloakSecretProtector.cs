namespace Fuse.Core.Areas.KeycloakIntegration;

public interface IKeycloakSecretProtector
{
    string Protect(string secret);
    string Unprotect(string protectedSecret);
}
