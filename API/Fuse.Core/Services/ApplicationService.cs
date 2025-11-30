using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class ApplicationService : IApplicationService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;
    private readonly IAuditService _auditService;
    private readonly IEnvironmentService _environmentService;

    public ApplicationService(IFuseStore fuseStore, ITagService tagService, IAuditService auditService, IEnvironmentService environmentService)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
        _auditService = auditService;
        _environmentService = environmentService;
    }

    public async Task<IReadOnlyList<Application>> GetApplicationsAsync() => (await _fuseStore.GetAsync()).Applications;

    public async Task<Application?> GetApplicationByIdAsync(Guid id) => (await _fuseStore.GetAsync()).Applications.FirstOrDefault(a => a.Id == id);

    public async Task<Result<Application>> CreateApplicationAsync(CreateApplication command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<Application>.Failure("Application name cannot be empty.", ErrorType.Validation);

        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
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
            Icon: command.Icon,
            TagIds: tagIds,
            Instances: Array.Empty<ApplicationInstance>(),
            Pipelines: Array.Empty<ApplicationPipeline>(),
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Append(app).ToList() });
        
        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ApplicationCreated,
            AuditArea.Application,
            "System",
            null,
            app.Id,
            new { app.Id, app.Name, app.Version, app.Owner }
        );
        await _auditService.LogAsync(auditLog);
        
        // Apply environment automation to create instances for environments with AutoCreateInstances enabled
        await _environmentService.ApplyEnvironmentAutomationAsync(new ApplyEnvironmentAutomation(ApplicationId: app.Id));
        
        // Return the updated application with any auto-created instances
        // This should always succeed since we just created the app, but we fetch it to get the latest state
        var updatedApp = await GetApplicationByIdAsync(app.Id);
        return Result<Application>.Success(updatedApp!);
    }

    public async Task<Result<Application>> UpdateApplicationAsync(UpdateApplication command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<Application>.Failure("Application name cannot be empty.", ErrorType.Validation);

        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
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
            Icon = command.Icon,
            TagIds = tagIds,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == command.Id ? updated : x).ToList() });
        
        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ApplicationUpdated,
            AuditArea.Application,
            "System",
            null,
            updated.Id,
            new { updated.Id, updated.Name, updated.Version, updated.Owner }
        );
        await _auditService.LogAsync(auditLog);
        
        return Result<Application>.Success(updated);
    }

    public async Task<Result> DeleteApplicationAsync(DeleteApplication command)
    {
        var store = await _fuseStore.GetAsync();
        var appToDelete = store.Applications.FirstOrDefault(a => a.Id == command.Id);
        if (appToDelete is null)
            return Result.Failure($"Application with ID '{command.Id}' not found.", ErrorType.NotFound);

        // Collect instance IDs for dependency scrubbing
        var deletedInstanceIds = appToDelete.Instances.Select(i => i.Id).ToHashSet();

        await _fuseStore.UpdateAsync(s =>
        {
            var apps = new List<Application>();
            foreach (var a in s.Applications)
            {
                if (a.Id == command.Id) continue; // remove the application entirely

                var instances = new List<ApplicationInstance>();
                var anyInstanceChanged = false;
                foreach (var inst in a.Instances)
                {
                    var filteredDeps = inst.Dependencies
                        .Where(d => !(d.TargetKind == TargetKind.Application && d.TargetId != Guid.Empty && deletedInstanceIds.Contains(d.TargetId)))
                        .ToList();
                    if (filteredDeps.Count != inst.Dependencies.Count)
                    {
                        anyInstanceChanged = true;
                        instances.Add(inst with { Dependencies = filteredDeps, UpdatedAt = DateTime.UtcNow });
                    }
                    else
                    {
                        instances.Add(inst);
                    }
                }
                var updatedA = a with { Instances = instances, UpdatedAt = anyInstanceChanged ? DateTime.UtcNow : a.UpdatedAt };
                apps.Add(updatedA);
            }
            return s with { Applications = apps };
        });
        
        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ApplicationDeleted,
            AuditArea.Application,
            "System",
            null,
            appToDelete.Id,
            new { appToDelete.Id, appToDelete.Name }
        );
        await _auditService.LogAsync(auditLog);
        
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

        if (command.PlatformId is Guid pid)
        {
            var platform = store.Platforms.FirstOrDefault(s => s.Id == pid);
            if (platform is null)
                return Result<ApplicationInstance>.Failure($"Platform with ID '{pid}' not found.", ErrorType.Validation);
        }

        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<ApplicationInstance>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        var now = DateTime.UtcNow;
        var inst = new ApplicationInstance(
            Id: Guid.NewGuid(),
            EnvironmentId: command.EnvironmentId,
            PlatformId: command.PlatformId,
            BaseUri: command.BaseUri,
            HealthUri: command.HealthUri,
            OpenApiUri: command.OpenApiUri,
            Version: command.Version,
            Dependencies: Array.Empty<ApplicationInstanceDependency>(),
            TagIds: tagIds,
            CreatedAt: now,
            UpdatedAt: now
        );

        var updated = app with { Instances = app.Instances.Append(inst).ToList(), UpdatedAt = now };
        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == app.Id ? updated : x).ToList() });
        
        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ApplicationInstanceCreated,
            AuditArea.Application,
            "System",
            null,
            inst.Id,
            new { ApplicationId = app.Id, ApplicationName = app.Name, InstanceId = inst.Id, inst.EnvironmentId }
        );
        await _auditService.LogAsync(auditLog);
        
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
        if (command.PlatformId is Guid pid)
        {
            var platform = store.Platforms.FirstOrDefault(s => s.Id == pid);
            if (platform is null)
                return Result<ApplicationInstance>.Failure($"Platform with ID '{pid}' not found.", ErrorType.Validation);
        }

        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<ApplicationInstance>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        var updatedInst = inst with
        {
            EnvironmentId = command.EnvironmentId,
            PlatformId = command.PlatformId,
            BaseUri = command.BaseUri,
            HealthUri = command.HealthUri,
            OpenApiUri = command.OpenApiUri,
            Version = command.Version,
            TagIds = tagIds,
            UpdatedAt = DateTime.UtcNow
        };

        var updatedApp = app with { Instances = app.Instances.Select(i => i.Id == inst.Id ? updatedInst : i).ToList(), UpdatedAt = DateTime.UtcNow };
        await _fuseStore.UpdateAsync(s => s with { Applications = s.Applications.Select(x => x.Id == app.Id ? updatedApp : x).ToList() });
        
        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ApplicationInstanceUpdated,
            AuditArea.Application,
            "System",
            null,
            updatedInst.Id,
            new { ApplicationId = app.Id, ApplicationName = app.Name, InstanceId = updatedInst.Id, updatedInst.EnvironmentId }
        );
        await _auditService.LogAsync(auditLog);
        
        return Result<ApplicationInstance>.Success(updatedInst);
    }

    public async Task<Result> DeleteInstanceAsync(DeleteApplicationInstance command)
    {
        var store = await _fuseStore.GetAsync();
        var app = store.Applications.FirstOrDefault(a => a.Id == command.ApplicationId);
        if (app is null)
            return Result.Failure($"Application with ID '{command.ApplicationId}' not found.", ErrorType.NotFound);
        var instance = app.Instances.FirstOrDefault(i => i.Id == command.InstanceId);
        if (instance is null)
            return Result.Failure($"Instance with ID '{command.InstanceId}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s =>
        {
            var apps = new List<Application>();
            foreach (var a in s.Applications)
            {
                var instances = new List<ApplicationInstance>();
                var anyInstanceChanged = false;
                foreach (var inst in a.Instances)
                {
                    if (a.Id == app.Id && inst.Id == command.InstanceId)
                    {
                        // skip (delete) this instance
                        continue;
                    }
                    var filteredDeps = inst.Dependencies
                        .Where(d => !(d.TargetKind == TargetKind.Application && d.TargetId == command.InstanceId))
                        .ToList();
                    if (filteredDeps.Count != inst.Dependencies.Count)
                    {
                        anyInstanceChanged = true;
                        instances.Add(inst with { Dependencies = filteredDeps, UpdatedAt = DateTime.UtcNow });
                    }
                    else
                    {
                        instances.Add(inst);
                    }
                }
                var updatedA = a with { Instances = instances, UpdatedAt = anyInstanceChanged || a.Id == app.Id ? DateTime.UtcNow : a.UpdatedAt };
                apps.Add(updatedA);
            }
            return s with { Applications = apps };
        });
        
        // Audit log
        var auditLog = AuditHelper.CreateLog(
            AuditAction.ApplicationInstanceDeleted,
            AuditArea.Application,
            "System",
            null,
            instance.Id,
            new { ApplicationId = app.Id, ApplicationName = app.Name, InstanceId = instance.Id, instance.EnvironmentId }
        );
        await _auditService.LogAsync(auditLog);
        
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

        // Validate auth references based on authKind
        var authValidation = ValidateDependencyAuth(store, command.AuthKind, command.AccountId, command.IdentityId, inst.Id, command.TargetKind, command.TargetId);
        if (authValidation is not null)
            return authValidation;

        if (command.Port is int p && (p < 1 || p > 65535))
            return Result<ApplicationInstanceDependency>.Failure("Port must be between 1 and 65535.", ErrorType.Validation);

        var dep = new ApplicationInstanceDependency(Guid.NewGuid(), command.TargetId, command.TargetKind, command.Port, command.AuthKind, command.AccountId, command.IdentityId);
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

        // Validate auth references based on authKind
        var authValidation = ValidateDependencyAuth(store, command.AuthKind, command.AccountId, command.IdentityId, inst.Id, command.TargetKind, command.TargetId);
        if (authValidation is not null)
            return authValidation;

        if (command.Port is int p && (p < 1 || p > 65535))
            return Result<ApplicationInstanceDependency>.Failure("Port must be between 1 and 65535.", ErrorType.Validation);

        var updatedDep = new ApplicationInstanceDependency(command.DependencyId, command.TargetId, command.TargetKind, command.Port, command.AuthKind, command.AccountId, command.IdentityId);
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

    private static Result<ApplicationInstanceDependency>? ValidateDependencyAuth(Snapshot store, DependencyAuthKind authKind, Guid? accountId, Guid? identityId, Guid instanceId, TargetKind dependencyTargetKind, Guid dependencyTargetId)
    {
        switch (authKind)
        {
            case DependencyAuthKind.Account:
                if (accountId is null)
                    return Result<ApplicationInstanceDependency>.Failure("AccountId is required when AuthKind is Account.", ErrorType.Validation);
                var account = store.Accounts.FirstOrDefault(a => a.Id == accountId);
                if (account is null)
                    return Result<ApplicationInstanceDependency>.Failure($"Account with ID '{accountId}' not found.", ErrorType.Validation);
                // Validate that the account's target matches the dependency's target
                if (account.TargetKind != dependencyTargetKind || account.TargetId != dependencyTargetId)
                    return Result<ApplicationInstanceDependency>.Failure($"Account must target the same resource as the dependency. Account targets {account.TargetKind}/{account.TargetId}, but dependency targets {dependencyTargetKind}/{dependencyTargetId}.", ErrorType.Validation);
                break;

            case DependencyAuthKind.Identity:
                if (identityId is null)
                    return Result<ApplicationInstanceDependency>.Failure("IdentityId is required when AuthKind is Identity.", ErrorType.Validation);
                var identity = store.Identities.FirstOrDefault(i => i.Id == identityId);
                if (identity is null)
                    return Result<ApplicationInstanceDependency>.Failure($"Identity with ID '{identityId}' not found.", ErrorType.Validation);
                // Identity ownership rule: An identity can be used by a dependency if:
                // 1. The identity has no owner (OwnerInstanceId is null) - making it a shared/global identity usable by any instance
                // 2. The identity is owned by the same instance that's creating the dependency
                if (identity.OwnerInstanceId is not null && identity.OwnerInstanceId != instanceId)
                    return Result<ApplicationInstanceDependency>.Failure($"Identity with ID '{identityId}' is owned by a different instance.", ErrorType.Validation);
                break;

            case DependencyAuthKind.None:
            default:
                // No validation needed for None
                break;
        }

        return null;
    }

    private static bool TargetExists(Snapshot s, TargetKind kind, Guid id) => kind switch
    {
        // Treat Application targets as Application Instance IDs; allow fallback to legacy app IDs for backward compatibility
        TargetKind.Application => s.Applications.SelectMany(a => a.Instances).Any(i => i.Id == id)
            || s.Applications.Any(a => a.Id == id),
        TargetKind.DataStore => s.DataStores.Any(d => d.Id == id),
        TargetKind.External => s.ExternalResources.Any(r => r.Id == id),
        _ => false
    };
}
