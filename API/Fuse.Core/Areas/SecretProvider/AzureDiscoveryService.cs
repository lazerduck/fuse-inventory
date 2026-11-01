using System.Text.Json;
using System.Text.RegularExpressions;
using Fuse.Core.Areas.Audit;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.SecretProvider;

public class AzureDiscoveryService : IAzureDiscoveryService
{
    private readonly IFuseStore _store;
    private readonly IAzureDiscoveryClient _client;
    private readonly IAuditService _audit;

    public AzureDiscoveryService(IFuseStore store, IAzureDiscoveryClient client, IAuditService audit)
    {
        _store = store;
        _client = client;
        _audit = audit;
    }

    public async Task<Result<AzureSubscriptionsResponse>> ListSubscriptionsAsync(SecretProviderAuthMode authMode, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(authMode)) return Result<AzureSubscriptionsResponse>.Failure("Select a supported authentication mode.");
        try { return Result<AzureSubscriptionsResponse>.Success(await _client.ListSubscriptionsAsync(authMode, ct)); }
        catch (Exception ex) when (AzureDiscoveryClient.IsRecoverable(ex, ct))
        { return Result<AzureSubscriptionsResponse>.Failure(AzureDiscoveryClient.FriendlyError(ex)); }
    }

    public async Task<Result<AzureResourcesResponse>> DiscoverAsync(DiscoverAzureResources command, CancellationToken ct = default)
    {
        var error = ValidateScope(command);
        if (error is not null) return Result<AzureResourcesResponse>.Failure(error);
        try
        {
            var response = await _client.DiscoverAsync(command, ct);
            var snapshot = await _store.GetAsync(ct);
            var excluded = new HashSet<string>(snapshot.AzureIntegrationManager?.ExcludedResourceIds ?? [], StringComparer.OrdinalIgnoreCase);
            var resources = response.Resources.Select(r => r with
            {
                AlreadyAdded = snapshot.SecretProviders.Any(p => Matches(p, r)),
                Ignored = excluded.Contains(r.ResourceId)
            }).Where(r => command.IncludeIgnored || !r.Ignored).ToList();
            return Result<AzureResourcesResponse>.Success(response with { Resources = resources });
        }
        catch (Exception ex) when (AzureDiscoveryClient.IsRecoverable(ex, ct))
        { return Result<AzureResourcesResponse>.Failure(AzureDiscoveryClient.FriendlyError(ex)); }
    }

    public async Task<Result<AzureImportResponse>> ImportAsync(ImportAzureResources command, string userName, Guid? userId, CancellationToken ct = default)
    {
        if (command.ResourceIds is not { Count: > 0 and <= 5000 } || command.ResourceIds.Any(id => !IsResourceId(id)))
            return Result<AzureImportResponse>.Failure("Select between 1 and 5,000 valid Azure resources to add.");
        var discovery = await DiscoverAsync(new(command.AuthMode, command.SubscriptionIds, command.ResourceGroups, command.Kinds, true), ct);
        if (!discovery.IsSuccess) return Result<AzureImportResponse>.Failure(discovery.Error!);
        var available = discovery.Value!.Resources.ToDictionary(r => r.ResourceId, StringComparer.OrdinalIgnoreCase);
        var results = new List<AzureResourceImportResult>();
        foreach (var id in command.ResourceIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            if (!available.TryGetValue(id, out var resource) || !IsEndpoint(resource.Endpoint, resource.Kind))
            {
                results.Add(new(id, available.GetValueOrDefault(id)?.Name ?? id, "Failed", "This resource was not discoverable in the selected scope. Scan again or check Azure permissions."));
                continue;
            }
            Models.SecretProvider? addedProvider = null;
            // Finish each local commit and its audit even if the HTTP request is disconnected.
            // Cancellation is honored before starting the next resource.
            // Check and append inside the store's write lock, including simultaneous import requests.
            await _store.UpdateAsync(snapshot =>
            {
                var existing = snapshot.SecretProviders.FirstOrDefault(p => Matches(p, resource));
                var exclusions = (snapshot.AzureIntegrationManager?.ExcludedResourceIds ?? [])
                    .Where(excluded => !excluded.Equals(resource.ResourceId, StringComparison.OrdinalIgnoreCase)).ToList();
                var manager = (snapshot.AzureIntegrationManager ?? new AzureIntegrationManager(null, DateTime.UtcNow)) with { ExcludedResourceIds = exclusions };
                if (existing is not null)
                {
                    results.Add(new(resource.ResourceId, existing.Name, "AlreadyAdded"));
                    return snapshot with { AzureIntegrationManager = manager };
                }
                var now = DateTime.UtcNow;
                var provider = new Models.SecretProvider(Guid.NewGuid(), resource.Name, new Uri(resource.Endpoint),
                    command.AuthMode, null, SecretProviderCapabilities.Check, now, now, resource.ResourceId);
                addedProvider = provider;
                results.Add(new(resource.ResourceId, provider.Name, "Added"));
                return snapshot with { SecretProviders = snapshot.SecretProviders.Append(provider).ToList(), AzureIntegrationManager = manager };
            }, CancellationToken.None);
            if (addedProvider is { } added)
                await _audit.LogAsync(new AuditLog(Guid.NewGuid(), DateTime.UtcNow, AuditAction.SecretProviderCreated,
                    AuditArea.SecretProvider, userName, userId, added.Id,
                    JsonSerializer.Serialize(new { added.Name, added.AzureResourceId, Source = "Azure discovery", Capabilities = "Check" })), CancellationToken.None);
        }
        return Result<AzureImportResponse>.Success(new(results, discovery.Value.Warnings));
    }

    public async Task<Result<AzureDiscoveryExclusionsResponse>> UpdateExclusionsAsync(UpdateAzureDiscoveryExclusions command, CancellationToken ct = default)
    {
        if (command.ResourceIds is null || command.ResourceIds.Count > 5000 || command.ResourceIds.Any(id => !IsResourceId(id)))
            return Result<AzureDiscoveryExclusionsResponse>.Failure("Provide up to 5,000 valid Azure resource IDs.");
        IReadOnlyList<string> saved = [];
        await _store.UpdateAsync(snapshot =>
        {
            var manager = snapshot.AzureIntegrationManager ?? new AzureIntegrationManager(null, DateTime.UtcNow);
            var exclusions = new HashSet<string>(manager.ExcludedResourceIds ?? [], StringComparer.OrdinalIgnoreCase);
            if (command.Excluded) exclusions.UnionWith(command.ResourceIds);
            else exclusions.ExceptWith(command.ResourceIds);
            saved = exclusions.Order(StringComparer.OrdinalIgnoreCase).ToList();
            return snapshot with { AzureIntegrationManager = manager with { ExcludedResourceIds = saved } };
        }, ct);
        return Result<AzureDiscoveryExclusionsResponse>.Success(new(saved));
    }

    private static string? ValidateScope(DiscoverAzureResources command)
    {
        if (!Enum.IsDefined(command.AuthMode)) return "Select a supported authentication mode.";
        if (command.SubscriptionIds is not { Count: > 0 and <= 100 } || command.SubscriptionIds.Any(s => !Guid.TryParse(s, out _)))
            return "Select between 1 and 100 valid subscription IDs.";
        if (command.ResourceGroups is { Count: > 100 } || command.ResourceGroups?.Any(g => string.IsNullOrWhiteSpace(g) || g.Length > 90) == true)
            return "Provide up to 100 resource-group names of 1 to 90 characters.";
        if (command.Kinds is { Count: 0 } || command.Kinds?.Any(k => k is not ("KeyVault" or "AppConfiguration")) == true)
            return "Select Key Vault, App Configuration, or both.";
        return null;
    }

    private static bool Matches(Models.SecretProvider provider, DiscoveredAzureResource resource) =>
        string.Equals(provider.AzureResourceId, resource.ResourceId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.VaultUri.AbsoluteUri.TrimEnd('/'), resource.Endpoint.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    internal static bool IsResourceId(string? value) => value is not null && value.Length <= 2048 &&
        Regex.IsMatch(value, @"^/subscriptions/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/resourceGroups/[^/\s]+/providers/(Microsoft\.KeyVault/vaults|Microsoft\.AppConfiguration/configurationStores)/[a-zA-Z0-9-]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool IsEndpoint(string? value, string kind)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/") return false;
        var suffix = kind == "KeyVault" ? ".vault.azure.net" : kind == "AppConfiguration" ? ".azconfig.io" : null;
        if (suffix is null || !uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;
        return Regex.IsMatch(uri.Host[..^suffix.Length], "^[a-zA-Z0-9-]+$");
    }
}
