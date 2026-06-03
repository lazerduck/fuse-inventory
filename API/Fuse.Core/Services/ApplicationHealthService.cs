using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class ApplicationHealthService(IFuseStore fuseStore) : IApplicationHealthService
{
    public async Task<ApplicationHealth> GetApplicationHealth(Guid applicationId)
    {
        var store = await fuseStore.GetAsync();
        var application = store.Applications.FirstOrDefault(a => a.Id == applicationId);
        if (application == null)
        {
            throw new KeyNotFoundException($"Application with ID {applicationId} not found.");
        }
        
        var instanceHealths = application.Instances.Select(instance =>
            new ApplicationInstanceHealth(
                instance.Id,
                instance.PlatformId != null,
                instance.BaseUri != null,
                instance.HealthUri != null,
                instance.OpenApiUri != null,
                !string.IsNullOrEmpty(instance.Version),
                instance.Dependencies.Count,
                instance.TagIds.Count
            )
        ).ToList();

        return new ApplicationHealth(
            application.Id,
            !string.IsNullOrEmpty(application.Version),
            !string.IsNullOrEmpty(application.Description),
            !string.IsNullOrEmpty(application.Owner),
            !string.IsNullOrEmpty(application.Framework),
            application.TagIds.Count,
            application.Pipelines.Count,
            instanceHealths
        );
    }

    public async Task<List<ApplicationHealth>> GetAllApplicationHealths()
    {
        var store = await fuseStore.GetAsync();
        var applications = store.Applications.ToList();

        return applications.Select(application =>
        {
            var instanceHealths = application.Instances.Select(instance =>
                new ApplicationInstanceHealth(
                    instance.Id,
                    instance.PlatformId != null,
                    instance.BaseUri != null,
                    instance.HealthUri != null,
                    instance.OpenApiUri != null,
                    !string.IsNullOrEmpty(instance.Version),
                    instance.Dependencies.Count,
                    instance.TagIds.Count
                )
            ).ToList();

            return new ApplicationHealth(
                application.Id,
                !string.IsNullOrEmpty(application.Version),
                !string.IsNullOrEmpty(application.Description),
                !string.IsNullOrEmpty(application.Owner),
                !string.IsNullOrEmpty(application.Framework),
                application.TagIds.Count,
                application.Pipelines.Count,
                instanceHealths
            );
        }).ToList();
    }
}