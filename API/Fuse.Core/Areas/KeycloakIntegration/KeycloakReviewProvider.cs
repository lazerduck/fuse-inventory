using System.Security.Cryptography;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.KeycloakIntegration;

public sealed class KeycloakReviewProvider(KeycloakInspector inspector, IKeycloakSecretProtector protector) : IIntegrationReviewProvider
{
    private static readonly ReviewField Client = new("client", "Client ID", Hint: "The exact client ID in this integration’s realm.");
    public string Provider => "keycloak";
    public IReadOnlyList<ReviewCheck> Checks { get; } = [
        new("role", "Service account role", [Client, new("role", "Role name"),
            new("owner", "Role owner's client ID", false, Hint: "Leave blank for a realm role."),
            new("basis", "Evaluate", Choices: ["assigned", "token-eligible"]),
            new("scope", "Requested optional scopes", false, Hint: "Space-separated. Leave blank for the default scopes."),
            new("expected", "Expected role", Choices: ["present", "absent"])]),
        new("client-setting", "Client configuration", [Client, new("setting", "Setting", Choices: [
            "redirectUris", "webOrigins", "enabled", "publicClient", "standardFlowEnabled", "implicitFlowEnabled",
            "directAccessGrantsEnabled", "serviceAccountsEnabled", "fullScopeAllowed"]),
            new("expected", "Expected value", false, Hint: "Use true or false for switches. For origins and redirects, separate values with spaces or new lines. Leave blank for an empty list.")])
    ];

    public string? Validate(Snapshot snapshot, ReviewInput input)
    {
        var item = snapshot.KeycloakIntegrations.FirstOrDefault(i => i.Id == input.IntegrationId);
        if (item == null) return "Select a Keycloak integration.";
        var client = input.Fields.GetValueOrDefault("client");
        if (string.IsNullOrWhiteSpace(client) || client.Length > 255) return "Specify a client ID up to 255 characters.";
        if (input.AccountId is { } id)
        {
            var account = snapshot.Accounts.FirstOrDefault(a => a.Id == id);
            if (account?.AuthKind != AuthKind.OAuthClient || account.Parameters?.GetValueOrDefault("keycloakClientId") != client ||
                !Uri.TryCreate(account.Parameters?.GetValueOrDefault("keycloakIssuer"), UriKind.Absolute, out var issuer) ||
                !KeycloakRealm.Valid(issuer) || KeycloakRealm.Identity(issuer) != KeycloakRealm.Identity(item.Issuer))
                return "Select an OAuth account mapped to this issuer and client ID.";
        }
        if (input.Kind == "client-setting" && input.Fields.GetValueOrDefault("setting") is not ("redirectUris" or "webOrigins")
            && input.Fields.GetValueOrDefault("expected") is not ("true" or "false"))
            return "Use true or false for a client switch.";
        if (input.Fields.TryGetValue("scope", out var scope) && (scope.Length > 512 || scope.Any(char.IsControl)))
            return "Use space-separated optional scopes up to 512 characters.";
        return null;
    }

    public async Task<ReviewEvidence> InspectAsync(Snapshot snapshot, ReviewInput input, CancellationToken ct)
    {
        var item = snapshot.KeycloakIntegrations.Single(i => i.Id == input.IntegrationId);
        try
        {
            return await inspector.ReviewAsync(new(item.Issuer, item.ClientId, protector.Unprotect(item.ProtectedClientSecret)), input, ct);
        }
        catch (CryptographicException)
        {
            return new(ReviewStatus.Unknown, "The inspection credential cannot be opened.", []);
        }
    }
}
