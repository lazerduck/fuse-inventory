using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.AzureMetadata;

internal class ArmMetadataSession(HttpClient http, string token, CancellationToken caller, CancellationToken deadline,
    string version, StringComparison pathComparison = StringComparison.OrdinalIgnoreCase)
{
    // Used only to correlate directory object IDs. The credential supplied this token for ARM.
    internal Guid? TenantId { get; } = TokenTenant(token);

    private static Guid? TokenTenant(string bearer)
    {
        var parts = bearer.Split('.');
        if (parts.Length != 3 || parts[1].Length > 65536) return null;
        try
        {
            var data = parts[1].Replace('-', '+').Replace('_', '/');
            using var document = JsonDocument.Parse(Convert.FromBase64String(data.PadRight((data.Length + 3) / 4 * 4, '=')));
            return document.RootElement.TryGetProperty("tid", out var tid) && tid.ValueKind == JsonValueKind.String &&
                Guid.TryParse(tid.GetString(), out var value) ? value : null;
        }
        catch (Exception error) when (error is FormatException or JsonException or InvalidOperationException) { return null; }
    }

    internal const string Arm = "https://management.azure.com";
    private static readonly TokenRequestContext TokenScope = new(["https://management.azure.com/.default"]);

    internal static async Task<string> GetToken(IAzureIntegrationManagerService manager, ITokenCredentialFactory credentials, SecretProviderAuthMode mode, CancellationToken ct, Guid? profileId = null, string? resourceId = null)
    {
        SecretProviderCredentials? shared = null;
        if (profileId is { } id)
        {
            if (mode != SecretProviderAuthMode.ClientSecret || resourceId is null)
                throw new InspectionError("credential-profile-unavailable");
            shared = await manager.GetProfileCredentialsAsync(id, resourceId);
            if (shared is null) throw new InspectionError("credential-profile-unavailable");
        }
        else if (mode == SecretProviderAuthMode.ClientSecret)
        {
            shared = await manager.GetClientSecretCredentialsAsync();
        }
        if (mode == SecretProviderAuthMode.ClientSecret && (string.IsNullOrWhiteSpace(shared?.TenantId) ||
            string.IsNullOrWhiteSpace(shared.ClientId) || string.IsNullOrWhiteSpace(shared.ClientSecret)))
            throw new InspectionError("credentials-not-configured");
        return (await credentials.Create(mode, shared).GetTokenAsync(TokenScope, ct)).Token;
    }

    internal sealed class InspectionError(string status) : Exception { public string Status { get; } = status; }
    internal static string? Failure(Exception e, CancellationToken caller) => e switch
    {
        InspectionError failure => failure.Status,
        AuthenticationFailedException or ArgumentException => "authentication-failed",
        OperationCanceledException when !caller.IsCancellationRequested => "timeout",
        HttpRequestException => "unavailable",
        JsonException or FormatException or InvalidOperationException or KeyNotFoundException => "invalid-response",
        _ => null
    };

    internal sealed record PageList<T>(List<T> Items, string Status);
    internal static string? Text(JsonElement row, string key) =>
        row.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
    internal static string Required(JsonElement row, string key) =>
        Text(row, key) is { Length: > 0 } s ? s : throw new InspectionError("invalid-response");

    protected CancellationToken Caller { get; } = caller;
    private int remaining = 300;

    public async Task<JsonDocument> Read(Uri next)
    {
        deadline.ThrowIfCancellationRequested();
        if (remaining-- <= 0) throw new InspectionError("limit-reached");
        using var request = new HttpRequestMessage(HttpMethod.Get, next);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, deadline);
        if (!response.IsSuccessStatusCode) throw new InspectionError(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "authentication-failed", HttpStatusCode.Forbidden => "denied",
            HttpStatusCode.NotFound => "not-found-or-hidden", HttpStatusCode.TooManyRequests => "throttled",
            _ => "request-failed"
        });
        return JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(deadline));
    }

    public async Task<(T? Value, string Status)> Resource<T>(string path, Func<JsonElement, T> project) where T : class
    {
        try
        {
            using var json = await Read(new Uri(Arm + path + "?api-version=" + version));
            return (project(json.RootElement), "visible");
        }
        catch (Exception e) when (Failure(e, Caller) is { } status) { return (null, status); }
    }

    public async Task<PageList<T>> List<T>(string path, Func<JsonElement, T> project, bool firstPageOnly = false,
        string? apiVersion = null, string? filter = null)
    {
        var rows = new List<T>();
        var first = new Uri(Arm + path + "?api-version=" + (apiVersion ?? version) +
            (filter is null ? "" : "&$filter=" + Uri.EscapeDataString(filter)));
        Uri? next = first;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            while (next is not null)
            {
                deadline.ThrowIfCancellationRequested();
                if (visited.Count >= 100) throw new InspectionError("limit-reached");
                if (next.Scheme != first.Scheme || next.Authority != first.Authority || next.UserInfo.Length != 0 ||
                    next.Fragment.Length != 0 || !next.AbsolutePath.Equals(first.AbsolutePath, pathComparison)
                    || !visited.Add(next.AbsoluteUri)) throw new InspectionError("invalid-continuation");
                using var json = await Read(next);
                var root = json.RootElement;
                foreach (var entry in root.GetProperty("value").EnumerateArray())
                {
                    if (rows.Count >= 1000) throw new InspectionError("limit-reached");
                    rows.Add(project(entry));
                }
                var link = Text(root, "nextLink");
                if (firstPageOnly || string.IsNullOrEmpty(link)) break;
                if (!Uri.TryCreate(link, UriKind.Absolute, out next)) throw new InspectionError("invalid-continuation");
            }
            return new(rows, "visible");
        }
        catch (Exception e) when (Failure(e, Caller) is { } status) { return new(rows, status); }
    }
}
