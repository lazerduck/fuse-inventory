using Fuse.Core.Commands;
using Fuse.Core.Interfaces;
using Fuse.Core.Manifests;

namespace Fuse.Core.Services
{
    public class ServiceService : IServiceService
    {
        private readonly IDataRepository _dataRepository;
        private const string fileName = "ServiceManifests.json";
        public static List<ServiceManifest> _serviceManifests = new List<ServiceManifest>();

        public ServiceService(IDataRepository dataRepository)
        {
            _dataRepository = dataRepository;
            var manifests = _dataRepository.GetObjectAsync<List<ServiceManifest>>(fileName).Result;
            if (manifests == null)
            {
                _dataRepository.SaveObjectAsync(fileName, _serviceManifests).Wait();
            }
            else
            {
                _serviceManifests = manifests;
            }
        }

        public async Task<ServiceManifest> CreateServiceManifestAsync(CreateServiceCommand command)
        {
            var manifest = new ServiceManifest(
                Guid.NewGuid(),
                command.Name,
                command.Version,
                command.Description,
                command.Notes,
                command.Author,
                command.Framework,
                command.Type,
                command.RepositoryUri,
                DateTime.Now,
                DateTime.Now,
                command.DeploymentPipelines,
                command.Deployments,
                command.Tags
            );


            _serviceManifests.Add(manifest);
            await _dataRepository.SaveObjectAsync(fileName, _serviceManifests);
            return manifest;
        }

        public Task UpdateServiceManifestAsync(ServiceManifest manifest)
        {
            var existingManifest = _serviceManifests.FirstOrDefault(m => m.Id == manifest.Id)
                ?? throw new ArgumentException($"No manifest found with Id {manifest.Id}.");

            manifest = manifest with { CreatedAt = existingManifest.CreatedAt, UpdatedAt = DateTime.UtcNow };

            _serviceManifests.Remove(existingManifest);
            _serviceManifests.Add(manifest);
            return _dataRepository.SaveObjectAsync(fileName, _serviceManifests);
        }

        public Task<ServiceManifest?> GetServiceManifestAsync(Guid id)
        {
            var manifest = _serviceManifests.FirstOrDefault(m => m.Id == id);
            return Task.FromResult(manifest);
        }

        public Task<List<ServiceManifest>> GetAllServiceManifestsAsync()
        {
            return Task.FromResult(_serviceManifests);
        }

        public Task DeleteServiceManifestAsync(Guid id)
        {
            var manifest = _serviceManifests.FirstOrDefault(m => m.Id == id)
                ?? throw new ArgumentException($"No manifest found with Id {id}.");
            _serviceManifests.Remove(manifest);
            return _dataRepository.SaveObjectAsync(fileName, _serviceManifests);
        }
    }

}