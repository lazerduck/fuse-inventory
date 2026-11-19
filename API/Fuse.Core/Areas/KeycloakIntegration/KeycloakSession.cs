using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Fuse.Core.Commands;

namespace Fuse.Core.Areas.KeycloakIntegration;

// One inspection owns its token and request budget. The shared HTTP handler never carries credentials between realms.
internal sealed class KeycloakSession(HttpClient http, ValidateKeycloakConnection settings)
{
    private string? token;
    private int remaining = 500;
    public bool CanViewAllClients { get; private set; }

    public async Task Authenticate(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, KeycloakRealm.Identity(settings.Issuer) + "/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials",
                ["client_id"] = settings.ClientId, ["client_secret"] = settings.ClientSecret })
        };
        var response = await Send(request, ct);
        token = Text(response, "access_token");
        if (string.IsNullOrEmpty(token)) throw new InspectionFailure("invalid-response");
        // Coverage is a hint from the configured token endpoint. The Admin API independently authorizes every read.
        var parts = token.Split('.');
        if (parts.Length != 3) return;
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        using var claims = JsonDocument.Parse(Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '=')));
        var root = claims.RootElement;
        if (Text(root, "iss") != KeycloakRealm.Identity(settings.Issuer)) throw new InspectionFailure("issuer-mismatch");
        CanViewAllClients = root.TryGetProperty("resource_access", out var resources)
            && resources.TryGetProperty("realm-management", out var manager) && manager.TryGetProperty("roles", out var roles)
            && roles.EnumerateArray().Any(r => r.GetString() is "view-clients" or "manage-clients");
    }

    public async Task<JsonElement> Get(string path, CancellationToken ct)
    {
        if (remaining-- <= 0) throw new InspectionFailure("limit-reached");
        using var request = new HttpRequestMessage(HttpMethod.Get, KeycloakRealm.AdminBase(settings.Issuer) + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await Send(request, ct);
    }

    private async Task<JsonElement> Send(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InspectionFailure(response.StatusCode switch
            {
                HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized when request.Method == HttpMethod.Post => "authentication-failed",
                HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => "denied",
                HttpStatusCode.NotFound => "unavailable",
                _ => "request-failed"
            });
        using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
        return body.RootElement.Clone();
    }

    public static string? Text(JsonElement element, string key) =>
        element.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;

    public static string Required(JsonElement element, string key) =>
        Text(element, key) is { Length: > 0 } value ? value : throw new InspectionFailure("invalid-response");

    public static string Segment(string id) => id.Length > 0 && id is not ("." or "..") && !id.Any(ch => char.IsControl(ch) || ch is '/' or '\\' or '%')
        ? Uri.EscapeDataString(id) : throw new InspectionFailure("invalid-response");
}

internal sealed class InspectionFailure(string status) : Exception
{
    public string Status { get; } = status;
}
