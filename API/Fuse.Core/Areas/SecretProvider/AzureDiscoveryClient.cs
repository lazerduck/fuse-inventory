using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.SecretProvider;

/// <summary>Reads resource metadata only. No secret or configuration values are requested.</summary>
public class AzureDiscoveryClient : IAzureDiscoveryClient
{
    private const string ArmHost = "management.azure.com";
    private const string GraphUrl = "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01";
    private readonly HttpClient _http;
    private readonly IAzureIntegrationManagerService _manager;
    private readonly ITokenCredentialFactory _credentials;

    public AzureDiscoveryClient(HttpClient http, IAzureIntegrationManagerService manager, ITokenCredentialFactory? credentials = null)
    {
        _http = http;
        _manager = manager;
        _credentials = credentials ?? new DefaultTokenCredentialFactory();
    }

    public async Task<AzureSubscriptionsResponse> ListSubscriptionsAsync(SecretProviderAuthMode authMode, CancellationToken ct)
    {
        var credential = await GetCredentialAsync(authMode);
        var subscriptions = new List<AzureSubscriptionResponse>();
        var warnings = new List<string>();
        string? next = "https://management.azure.com/subscriptions?api-version=2022-12-01";
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (next is not null)
        {
            // Azure supplies nextLink, but credentials must never leave the ARM endpoint.
            if (!IsArmUrl(next) || !visited.Add(next) || visited.Count > 100)
            {
                warnings.Add("Azure returned an invalid or repeated subscription continuation link. The subscription list may be incomplete.");
                break;
            }
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, next);
                using var response = await SendAsync(request, credential, ct);
                var root = response.RootElement;
                foreach (var item in root.GetProperty("value").EnumerateArray())
                {
                    var id = Text(item, "subscriptionId");
                    if (Guid.TryParse(id, out var parsed))
                        subscriptions.Add(new(parsed.ToString(), Text(item, "displayName") ?? id!));
                }
                next = Text(root, "nextLink");
            }
            catch (Exception ex) when (IsRecoverable(ex, ct) && subscriptions.Count > 0)
            {
                warnings.Add($"The subscription list is incomplete: {FriendlyError(ex)}");
                break;
            }
        }
        return new(subscriptions.DistinctBy(s => s.Id).OrderBy(s => s.Name).ToList(), warnings);
    }

    public async Task<AzureResourcesResponse> DiscoverAsync(DiscoverAzureResources command, CancellationToken ct)
    {
        var credential = await GetCredentialAsync(command.AuthMode);
        var resources = new List<DiscoveredAzureResource>();
        var warnings = new List<string>();
        var query = BuildQuery(command);

        // Query each selected subscription so a failure in one does not hide successful results from another.
        foreach (var subscription in command.SubscriptionIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string? skipToken = null;
            var tokens = new HashSet<string>();
            try
            {
                for (var page = 0; page < 100; page++)
                {
                    var options = new Dictionary<string, object> { ["resultFormat"] = "objectArray", ["$top"] = 1000 };
                    if (skipToken is not null) options["$skipToken"] = skipToken;
                    using var request = new HttpRequestMessage(HttpMethod.Post, GraphUrl)
                    {
                        Content = JsonContent.Create(new { subscriptions = new[] { subscription }, query, options })
                    };
                    using var response = await SendAsync(request, credential, ct);
                    var root = response.RootElement;
                    foreach (var item in root.GetProperty("data").EnumerateArray())
                    {
                        var kind = Text(item, "type")?.ToLowerInvariant() switch
                        {
                            "microsoft.keyvault/vaults" => "KeyVault",
                            "microsoft.appconfiguration/configurationstores" => "AppConfiguration",
                            _ => null
                        };
                        var id = Text(item, "id");
                        var endpoint = Text(item, "endpoint");
                        if (kind is null || !AzureDiscoveryService.IsResourceId(id) ||
                            !AzureDiscoveryService.IsEndpoint(endpoint, kind) ||
                            !string.Equals(Text(item, "subscriptionId"), subscription, StringComparison.OrdinalIgnoreCase))
                        {
                            warnings.Add($"Skipped a resource with incomplete or unsupported metadata in subscription {subscription}.");
                            continue;
                        }
                        resources.Add(new(id!, Text(item, "name") ?? id!, kind, endpoint!, subscription,
                            Text(item, "resourceGroup") ?? "", Text(item, "location") ?? ""));
                    }
                    skipToken = Text(root, "$skipToken");
                    if (string.IsNullOrEmpty(skipToken))
                    {
                        if (Text(root, "resultTruncated")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
                            warnings.Add($"Azure truncated results for subscription {subscription}. Narrow the resource-group scope and scan again.");
                        break;
                    }
                    if (!tokens.Add(skipToken) || page == 99)
                    {
                        warnings.Add($"Discovery stopped before all resources were read in subscription {subscription}. Narrow the scope and scan again.");
                        break;
                    }
                }
            }
            catch (Exception ex) when (IsRecoverable(ex, ct))
            {
                warnings.Add($"Subscription {subscription}: {FriendlyError(ex)}");
            }
        }
        return new(resources.DistinctBy(r => r.ResourceId.ToLowerInvariant()).OrderBy(r => r.Name).ToList(), warnings.Distinct().ToList());
    }

    private async Task<TokenCredential> GetCredentialAsync(SecretProviderAuthMode mode)
    {
        var shared = mode == SecretProviderAuthMode.ClientSecret ? await _manager.GetClientSecretCredentialsAsync() : null;
        if (mode == SecretProviderAuthMode.ClientSecret &&
            (string.IsNullOrWhiteSpace(shared?.TenantId) || string.IsNullOrWhiteSpace(shared.ClientId) || string.IsNullOrWhiteSpace(shared.ClientSecret)))
            throw new InvalidOperationException("Configure shared Azure credentials before discovering resources with Client Secret authentication.");
        return _credentials.Create(mode, shared);
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, TokenCredential credential, CancellationToken ct)
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://management.azure.com/.default"]), ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Forbidden => "Discovery access was denied. Grant this Azure identity management-plane read access to the selected resources.",
                System.Net.HttpStatusCode.Unauthorized => "Azure rejected the credentials used for discovery.",
                System.Net.HttpStatusCode.TooManyRequests => "Azure throttled discovery. Retry the scan shortly.",
                _ => $"Azure discovery failed (HTTP {(int)response.StatusCode})."
            };
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    private static string BuildQuery(DiscoverAzureResources command)
    {
        var kinds = command.Kinds is { Count: > 0 } ? command.Kinds : new[] { "KeyVault", "AppConfiguration" };
        var types = kinds.Select(k => k == "KeyVault" ? "microsoft.keyvault/vaults" : "microsoft.appconfiguration/configurationstores");
        var query = $"Resources | where type in~ ({string.Join(",", types.Select(t => JsonSerializer.Serialize(t)))})";
        if (command.ResourceGroups is { Count: > 0 })
            query += $" | where resourceGroup in~ ({string.Join(",", command.ResourceGroups.Select(g => JsonSerializer.Serialize(g)))})";
        return query + " | project id, name, type, subscriptionId, resourceGroup, location, endpoint = iff(type =~ 'microsoft.keyvault/vaults', tostring(properties.vaultUri), tostring(properties.endpoint)) | order by id asc";
    }

    private static bool IsArmUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals(ArmHost, StringComparison.OrdinalIgnoreCase) &&
        uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);
    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null ? property.ToString() : null;
    internal static bool IsRecoverable(Exception ex, CancellationToken ct) =>
        !ct.IsCancellationRequested && ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or Azure.Identity.AuthenticationFailedException or Azure.RequestFailedException or TaskCanceledException;
    internal static string FriendlyError(Exception ex) => ex switch
    {
        Azure.Identity.AuthenticationFailedException => "Azure authentication failed. Check the configured identity and credentials.",
        TaskCanceledException => "Azure discovery timed out. Retry the scan.",
        JsonException or KeyNotFoundException => "Azure returned an unexpected discovery response.",
        _ => ex.Message
    };
}
