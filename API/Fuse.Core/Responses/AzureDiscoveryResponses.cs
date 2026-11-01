namespace Fuse.Core.Responses;

public record AzureSubscriptionResponse(string Id, string Name);
public record AzureSubscriptionsResponse(IReadOnlyList<AzureSubscriptionResponse> Subscriptions, IReadOnlyList<string> Warnings);
public record DiscoveredAzureResource(
    string ResourceId, string Name, string Kind, string Endpoint, string SubscriptionId,
    string ResourceGroup, string Location, bool AlreadyAdded = false, bool Ignored = false);
public record AzureResourcesResponse(IReadOnlyList<DiscoveredAzureResource> Resources, IReadOnlyList<string> Warnings);
public record AzureResourceImportResult(string ResourceId, string Name, string Status, string? Error = null);
public record AzureImportResponse(IReadOnlyList<AzureResourceImportResult> Results, IReadOnlyList<string> Warnings);
public record AzureDiscoveryExclusionsResponse(IReadOnlyList<string> ResourceIds);
public record SecretProviderAccessCheckResponse(bool Success, DateTime CheckedAt, string Message);
