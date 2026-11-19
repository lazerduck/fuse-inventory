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
    internal const string Arm = "https://management.azure.com";
    private static readonly TokenRequestContext TokenScope = new(["https://management.azure.com/.default"]);

    internal static async Task<string> GetToken(IAzureIntegrationManagerService manager, ITokenCredentialFactory credentials, SecretProviderAuthMode mode, CancellationToken ct)
    {
        var shared = mode == SecretProviderAuthMode.ClientSecret ? await manager.GetClientSecretCredentialsAsync() : null;
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

    public async Task<PageList<T>> List<T>(string path, Func<JsonElement, T> project, bool firstPageOnly = false)
    {
        var rows = new List<T>();
        var first = new Uri(Arm + path + "?api-version=" + version);
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
