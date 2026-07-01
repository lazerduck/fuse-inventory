using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using ApplicationModel = Fuse.Core.Models.Application;

namespace Fuse.Core.Areas.Application;

public interface IApplicationService
{
    // Applications
    Task<IReadOnlyList<ApplicationModel>> GetApplicationsAsync();
    Task<ApplicationModel?> GetApplicationByIdAsync(Guid id);
    Task<Result<ApplicationModel>> CreateApplicationAsync(CreateApplication command);
    Task<Result<ApplicationModel>> UpdateApplicationAsync(UpdateApplication command);
    Task<Result> DeleteApplicationAsync(DeleteApplication command);

    // Instances
    Task<Result<ApplicationInstance>> CreateInstanceAsync(CreateApplicationInstance command);
    Task<Result<ApplicationInstance>> UpdateInstanceAsync(UpdateApplicationInstance command);
    Task<Result> DeleteInstanceAsync(DeleteApplicationInstance command);
    Task<Result<string>> GetInstanceApiKeyAsync(Guid applicationId, Guid instanceId);

    // Pipelines
    Task<Result<ApplicationPipeline>> CreatePipelineAsync(CreateApplicationPipeline command);
    Task<Result<ApplicationPipeline>> UpdatePipelineAsync(UpdateApplicationPipeline command);
    Task<Result> DeletePipelineAsync(DeleteApplicationPipeline command);

    // Dependencies
    Task<Result<ApplicationInstanceDependency>> CreateDependencyAsync(CreateApplicationDependency command);
    Task<Result<ApplicationInstanceDependency>> UpdateDependencyAsync(UpdateApplicationDependency command);
    Task<Result> DeleteDependencyAsync(DeleteApplicationDependency command);
}
