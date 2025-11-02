using Fuse.Core.Commands;
using Fuse.Core.Manifests;

namespace Fuse.Core.Interfaces
{
    public interface IServiceService
    {
        Task<ServiceManifest> CreateServiceManifestAsync(CreateServiceCommand manifest);
        Task UpdateServiceManifestAsync(ServiceManifest manifest);
        Task<ServiceManifest?> GetServiceManifestAsync(Guid id);
        Task<List<ServiceManifest>> GetAllServiceManifestsAsync();
        Task DeleteServiceManifestAsync(Guid id);
    }
}