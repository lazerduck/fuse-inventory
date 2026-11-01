using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record AzureDiscoveryAuthentication(SecretProviderAuthMode AuthMode);

public record DiscoverAzureResources(
    SecretProviderAuthMode AuthMode,
    IReadOnlyList<string> SubscriptionIds,
    IReadOnlyList<string>? ResourceGroups = null,
    IReadOnlyList<string>? Kinds = null,
    bool IncludeIgnored = false);

public record ImportAzureResources(
    SecretProviderAuthMode AuthMode,
    IReadOnlyList<string> SubscriptionIds,
    IReadOnlyList<string> ResourceIds,
    IReadOnlyList<string>? ResourceGroups = null,
    IReadOnlyList<string>? Kinds = null);

public record UpdateAzureDiscoveryExclusions(IReadOnlyList<string> ResourceIds, bool Excluded);
