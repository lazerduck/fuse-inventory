using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IApplicationService
{
    // Applications
    Task<IReadOnlyList<Application>> GetApplicationsAsync();
    Task<Application?> GetApplicationByIdAsync(Guid id);
    Task<Result<Application>> CreateApplicationAsync(CreateApplication command);
    Task<Result<Application>> UpdateApplicationAsync(UpdateApplication command);
    Task<Result> DeleteApplicationAsync(DeleteApplication command);

    // Instances
    Task<Result<ApplicationInstance>> CreateInstanceAsync(CreateApplicationInstance command);
    Task<Result<ApplicationInstance>> UpdateInstanceAsync(UpdateApplicationInstance command);
    Task<Result> DeleteInstanceAsync(DeleteApplicationInstance command);

    // Pipelines
    Task<Result<ApplicationPipeline>> CreatePipelineAsync(CreateApplicationPipeline command);
    Task<Result<ApplicationPipeline>> UpdatePipelineAsync(UpdateApplicationPipeline command);
    Task<Result> DeletePipelineAsync(DeleteApplicationPipeline command);

    // Dependencies
    Task<Result<ApplicationInstanceDependency>> CreateDependencyAsync(CreateApplicationDependency command);
    Task<Result<ApplicationInstanceDependency>> UpdateDependencyAsync(UpdateApplicationDependency command);
    Task<Result> DeleteDependencyAsync(DeleteApplicationDependency command);
}
