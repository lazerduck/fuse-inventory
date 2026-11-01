using Fuse.Core.Commands;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.SecretProvider;

public interface IAzureDiscoveryClient
{
    Task<AzureSubscriptionsResponse> ListSubscriptionsAsync(SecretProviderAuthMode authMode, CancellationToken ct);
    Task<AzureResourcesResponse> DiscoverAsync(DiscoverAzureResources command, CancellationToken ct);
}
