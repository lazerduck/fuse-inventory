using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.SecretProvider;

public interface IAzureDiscoveryService
{
    Task<Result<AzureSubscriptionsResponse>> ListSubscriptionsAsync(SecretProviderAuthMode authMode, CancellationToken ct = default);
    Task<Result<AzureResourcesResponse>> DiscoverAsync(DiscoverAzureResources command, CancellationToken ct = default);
    Task<Result<AzureImportResponse>> ImportAsync(ImportAzureResources command, string userName, Guid? userId, CancellationToken ct = default);
    Task<Result<AzureDiscoveryExclusionsResponse>> UpdateExclusionsAsync(UpdateAzureDiscoveryExclusions command, CancellationToken ct = default);
}
