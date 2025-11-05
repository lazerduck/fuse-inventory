using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class ApplicationService : IApplicationService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;

    public ApplicationService(IFuseStore fuseStore, ITagService tagService)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
    }

    public async Task<IReadOnlyList<Application>> GetApplicationsAsync() => (await _fuseStore.GetAsync()).Applications;

    public async Task<Application?> GetApplicationByIdAsync(Guid id) => (await _fuseStore.GetAsync()).Applications.FirstOrDefault(a => a.Id == id);

    public async Task<Result<Application>> CreateApplicationAsync(CreateApplication command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<Application>.Failure("Application name cannot be empty.", ErrorType.Validation);

        foreach (var tagId in command.TagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Application>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        var store = await _fuseStore.GetAsync();
        if (store.Applications.Any(a => string.Equals(a.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<Application>.Failure($"Application with name '{command.Name}' already exists.", ErrorType.Conflict);

        var now = DateTime.UtcNow;
        var app = new Application(
            Id: Guid.NewGuid(),
            Name: command.Name,
            Version: command.Version,
            Description: command.Description,
            Owner: command.Owner,
            Notes: command.Notes,
            Framework: command.Framework,
            RepositoryUri: command.RepositoryUri,
            TagIds: command.TagIds,
            Instances: Array.Empty<ApplicationInstance>(),
            Pipelines: Array.Empty<ApplicationPipeline>(),
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Append(app).ToList() });
        return Result<Application>.Success(app);
    }

    public async Task<Result<Application>> UpdateApplicationAsync(UpdateApplication command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<Application>.Failure("Application name cannot be empty.", ErrorType.Validation);

        foreach (var tagId in command.TagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Application>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        var store = await _fuseStore.GetAsync();
        var existing = store.Applications.FirstOrDefault(a => a.Id == command.Id);
        if (existing is null)
            return Result<Application>.Failure($"Application with ID '{command.Id}' not found.", ErrorType.NotFound);

        if (store.Applications.Any(a => a.Id != command.Id && string.Equals(a.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<Application>.Failure($"Application with name '{command.Name}' already exists.", ErrorType.Conflict);

        var updated = existing with
        {
            Name = command.Name,
            Version = command.Version,
            Description = command.Description,
            Owner = command.Owner,
            Notes = command.Notes,
            Framework = command.Framework,
            RepositoryUri = command.RepositoryUri,
            TagIds = command.TagIds,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == command.Id ? updated : x).ToList() });
        return Result<Application>.Success(updated);
    }

    public async Task<Result> DeleteApplicationAsync(DeleteApplication command)
    {
        var store = await _fuseStore.GetAsync();
        if (!store.Applications.Any(a => a.Id == command.Id))
            return Result.Failure($"Application with ID '{command.Id}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Where(x => x.Id != command.Id).ToList() });
        return Result.Success();
    }

    public async Task<Result<ApplicationInstance>> CreateInstanceAsync(CreateApplicationInstance command)
    {
        var store = await _fuseStore.GetAsync();
        var app = store.Applications.FirstOrDefault(a => a.Id == command.ApplicationId);
        if (app is null)
            return Result<ApplicationInstance>.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.NotFound);

        if (!store.Environments.Any(e => e.Id == command.EnvironmentId))
            return Result<ApplicationInstance>.Failure($"Environment with ID '{command.EnvironmentId}' not found.", ErrorType.Validation);

        if (command.ServerId is Guid sid)
        {
            var server = store.Servers.FirstOrDefault(s => s.Id == sid);
            if (server is null)
                return Result<ApplicationInstance>.Failure($"Server with ID '{sid}' not found.", ErrorType.Validation);
            if (server.EnvironmentId != command.EnvironmentId)
                return Result<ApplicationInstance>.Failure("Server and instance environment must match.", ErrorType.Validation);
        }

        foreach (var tagId in command.TagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<ApplicationInstance>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        var now = DateTime.UtcNow;
        var inst = new ApplicationInstance(
            Id: Guid.NewGuid(),
            EnvironmentId: command.EnvironmentId,
            ServerId: command.ServerId,
            BaseUri: command.BaseUri,
            HealthUri: command.HealthUri,
            OpenApiUri: command.OpenApiUri,
            Version: command.Version,
            Dependencies: Array.Empty<ApplicationInstanceDependency>(),
            TagIds: command.TagIds,
            CreatedAt: now,
            UpdatedAt: now
        );

        var updated = app with { Instances = app.Instances.Append(inst).ToList(), UpdatedAt = now };
        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == app.Id ? updated : x).ToList() });
        return Result<ApplicationInstance>.Success(inst);
    }

    public async Task<Result<ApplicationInstance>> UpdateInstanceAsync(UpdateApplicationInstance command)
    {
        var store = await _fuseStore.GetAsync();
        var app = store.Applications.FirstOrDefault(a => a.Id == command.ApplicationId);
        if (app is null)
            return Result<ApplicationInstance>.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.NotFound);

        var inst = app.Instances.FirstOrDefault(i => i.Id == command.InstanceId);
        if (inst is null)
            return Result<ApplicationInstance>.Failure($"Instance with ID '{command.InstanceId}' not found.", ErrorType.NotFound);

        if (!store.Environments.Any(e => e.Id == command.EnvironmentId))
            return Result<ApplicationInstance>.Failure($"Environment with ID '{command.EnvironmentId}' not found.", ErrorType.Validation);
        if (command.ServerId is Guid sid)
        {
            var server = store.Servers.FirstOrDefault(s => s.Id == sid);
            if (server is null)
                return Result<ApplicationInstance>.Failure($"Server with ID '{sid}' not found.", ErrorType.Validation);
            if (server.EnvironmentId != command.EnvironmentId)
                return Result<ApplicationInstance>.Failure("Server and instance environment must match.", ErrorType.Validation);
        }

        foreach (var tagId in command.TagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<ApplicationInstance>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        var updatedInst = inst with
        {
            EnvironmentId = command.EnvironmentId,
            ServerId = command.ServerId,
            BaseUri = command.BaseUri,
            HealthUri = command.HealthUri,
            OpenApiUri = command.OpenApiUri,
            Version = command.Version,
            TagIds = command.TagIds,
            UpdatedAt = DateTime.UtcNow
        };

        var updatedApp = app with { Instances = app.Instances.Select(i => i.Id == inst.Id ? updatedInst : i).ToList(), UpdatedAt = DateTime.UtcNow };
        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == app.Id ? updatedApp : x).ToList() });
        return Result<ApplicationInstance>.Success(updatedInst);
    }

    public async Task<Result> DeleteInstanceAsync(DeleteApplicationInstance command)
    {
        var store = await _fuseStore.GetAsync();
        var app = store.Applications.FirstOrDefault(a => a.Id == command.ApplicationId);
        if (app is null)
            return Result.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.NotFound);
        if (!app.Instances.Any(i => i.Id == command.InstanceId))
            return Result.Failure($"Instance with ID '{command.InstanceId}' not found.", ErrorType.NotFound);

        var updatedApp = app with { Instances = app.Instances.Where(i => i.Id != command.InstanceId).ToList(), UpdatedAt = DateTime.UtcNow };
        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == app.Id ? updatedApp : x).ToList() });
        return Result.Success();
    }

    public async Task<Result<ApplicationPipeline>> CreatePipelineAsync(CreateApplicationPipeline command)
    {
        var store = await _fuseStore.GetAsync();
        var app = store.Applications.FirstOrDefault(a => a.Id == command.ApplicationId);
        if (app is null)
            return Result<ApplicationPipeline>.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.NotFound);

        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<ApplicationPipeline>.Failure("Pipeline name cannot be empty.", ErrorType.Validation);
        if (app.Pipelines.Any(p => string.Equals(p.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<ApplicationPipeline>.Failure($"Pipeline with name '{command.Name}' already exists for the application.", ErrorType.Conflict);

        var pipe = new ApplicationPipeline(Guid.NewGuid(), command.Name, command.PipelineUri);
        var updatedApp = app with { Pipelines = app.Pipelines.Append(pipe).ToList(), UpdatedAt = DateTime.UtcNow };
        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == app.Id ? updatedApp : x).ToList() });
        return Result<ApplicationPipeline>.Success(pipe);
    }

    public async Task<Result<ApplicationPipeline>> UpdatePipelineAsync(UpdateApplicationPipeline command)
    {
        var store = await _fuseStore.GetAsync();
        var app = store.Applications.FirstOrDefault(a => a.Id == command.ApplicationId);
        if (app is null)
            return Result<ApplicationPipeline>.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.NotFound);

        var pipe = app.Pipelines.FirstOrDefault(p => p.Id == command.PipelineId);
        if (pipe is null)
            return Result<ApplicationPipeline>.Failure($"Pipeline with ID '{command.PipelineId}' not found.", ErrorType.NotFound);

        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<ApplicationPipeline>.Failure("Pipeline name cannot be empty.", ErrorType.Validation);
        if (app.Pipelines.Any(p => p.Id != command.PipelineId && string.Equals(p.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<ApplicationPipeline>.Failure($"Pipeline with name '{command.Name}' already exists for the application.", ErrorType.Conflict);

        var updatedPipe = new ApplicationPipeline(command.PipelineId, command.Name, command.PipelineUri);
        var updatedApp = app with { Pipelines = app.Pipelines.Select(p => p.Id == pipe.Id ? updatedPipe : p).ToList(), UpdatedAt = DateTime.UtcNow };
        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == app.Id ? updatedApp : x).ToList() });
        return Result<ApplicationPipeline>.Success(updatedPipe);
    }

    public async Task<Result> DeletePipelineAsync(DeleteApplicationPipeline command)
    {
        var store = await _fuseStore.GetAsync();
        var app = store.Applications.FirstOrDefault(a => a.Id == command.ApplicationId);
        if (app is null)
            return Result.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.NotFound);
        if (!app.Pipelines.Any(p => p.Id == command.PipelineId))
            return Result.Failure($"Pipeline with ID '{command.PipelineId}' not found.", ErrorType.NotFound);

        var updatedApp = app with { Pipelines = app.Pipelines.Where(p => p.Id != command.PipelineId).ToList(), UpdatedAt = DateTime.UtcNow };
        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == app.Id ? updatedApp : x).ToList() });
        return Result.Success();
    }

    public async Task<Result<ApplicationInstanceDependency>> CreateDependencyAsync(CreateApplicationDependency command)
    {
        var store = await _fuseStore.GetAsync();
        var app = store.Applications.FirstOrDefault(a => a.Id == command.ApplicationId);
        if (app is null)
            return Result<ApplicationInstanceDependency>.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.NotFound);
        var inst = app.Instances.FirstOrDefault(i => i.Id == command.InstanceId);
        if (inst is null)
            return Result<ApplicationInstanceDependency>.Failure($"Instance with ID '{command.InstanceId}' not found.", ErrorType.NotFound);

        if (!TargetExists(store, command.TargetKind, command.TargetId))
            return Result<ApplicationInstanceDependency>.Failure($"Target {command.TargetKind}/{command.TargetId} not found.", ErrorType.Validation);

        if (command.AccountId is Guid aid && !store.Accounts.Any(a => a.Id == aid))
            return Result<ApplicationInstanceDependency>.Failure($"Account with ID '{aid}' not found.", ErrorType.Validation);

        if (command.Port is int p && (p < 1 || p > 65535))
            return Result<ApplicationInstanceDependency>.Failure("Port must be between 1 and 65535.", ErrorType.Validation);

        var dep = new ApplicationInstanceDependency(Guid.NewGuid(), command.TargetId, command.TargetKind, command.Port, command.AccountId);
        var updatedInst = inst with { Dependencies = inst.Dependencies.Append(dep).ToList(), UpdatedAt = DateTime.UtcNow };
        var updatedApp = app with { Instances = app.Instances.Select(i => i.Id == inst.Id ? updatedInst : i).ToList(), UpdatedAt = DateTime.UtcNow };
        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == app.Id ? updatedApp : x).ToList() });
        return Result<ApplicationInstanceDependency>.Success(dep);
    }

    public async Task<Result<ApplicationInstanceDependency>> UpdateDependencyAsync(UpdateApplicationDependency command)
    {
        var store = await _fuseStore.GetAsync();
        var app = store.Applications.FirstOrDefault(a => a.Id == command.ApplicationId);
        if (app is null)
            return Result<ApplicationInstanceDependency>.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.NotFound);
        var inst = app.Instances.FirstOrDefault(i => i.Id == command.InstanceId);
        if (inst is null)
            return Result<ApplicationInstanceDependency>.Failure($"Instance with ID '{command.InstanceId}' not found.", ErrorType.NotFound);
        var dep = inst.Dependencies.FirstOrDefault(d => d.Id == command.DependencyId);
        if (dep is null)
            return Result<ApplicationInstanceDependency>.Failure($"Dependency with ID '{command.DependencyId}' not found.", ErrorType.NotFound);

        if (!TargetExists(store, command.TargetKind, command.TargetId))
            return Result<ApplicationInstanceDependency>.Failure($"Target {command.TargetKind}/{command.TargetId} not found.", ErrorType.Validation);
        if (command.AccountId is Guid aid && !store.Accounts.Any(a => a.Id == aid))
            return Result<ApplicationInstanceDependency>.Failure($"Account with ID '{aid}' not found.", ErrorType.Validation);
        if (command.Port is int p && (p < 1 || p > 65535))
            return Result<ApplicationInstanceDependency>.Failure("Port must be between 1 and 65535.", ErrorType.Validation);

        var updatedDep = new ApplicationInstanceDependency(command.DependencyId, command.TargetId, command.TargetKind, command.Port, command.AccountId);
        var updatedInst = inst with { Dependencies = inst.Dependencies.Select(d => d.Id == dep.Id ? updatedDep : d).ToList(), UpdatedAt = DateTime.UtcNow };
        var updatedApp = app with { Instances = app.Instances.Select(i => i.Id == inst.Id ? updatedInst : i).ToList(), UpdatedAt = DateTime.UtcNow };
        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == app.Id ? updatedApp : x).ToList() });
        return Result<ApplicationInstanceDependency>.Success(updatedDep);
    }

    public async Task<Result> DeleteDependencyAsync(DeleteApplicationDependency command)
    {
        var store = await _fuseStore.GetAsync();
        var app = store.Applications.FirstOrDefault(a => a.Id == command.ApplicationId);
        if (app is null)
            return Result.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.NotFound);
        var inst = app.Instances.FirstOrDefault(i => i.Id == command.InstanceId);
        if (inst is null)
            return Result.Failure($"Instance with ID '{command.InstanceId}' not found.", ErrorType.NotFound);
        if (!inst.Dependencies.Any(d => d.Id == command.DependencyId))
            return Result.Failure($"Dependency with ID '{command.DependencyId}' not found.", ErrorType.NotFound);

        var updatedInst = inst with { Dependencies = inst.Dependencies.Where(d => d.Id != command.DependencyId).ToList(), UpdatedAt = DateTime.UtcNow };
        var updatedApp = app with { Instances = app.Instances.Select(i => i.Id == inst.Id ? updatedInst : i).ToList(), UpdatedAt = DateTime.UtcNow };
        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == app.Id ? updatedApp : x).ToList() });
        return Result.Success();
    }

    private static bool TargetExists(Snapshot s, TargetKind kind, Guid id) => kind switch
    {
        TargetKind.Application => s.Applications.Any(a => a.Id == id),
        TargetKind.DataStore => s.DataStores.Any(d => d.Id == id),
        TargetKind.External => s.ExternalResources.Any(r => r.Id == id),
        _ => false
    };
}
