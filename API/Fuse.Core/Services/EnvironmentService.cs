using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class EnvironmentService : IEnvironmentService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;

    public EnvironmentService(IFuseStore fuseStore, ITagService tagService)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
    }

    public async Task<Result<EnvironmentInfo>> CreateEnvironment(CreateEnvironment command)
    {
        if (command.Name == string.Empty)
        {
            return Result<EnvironmentInfo>.Failure("Environment name cannot be empty.", ErrorType.Validation);
        }
        
        var tagIds = command.TagIds ?? new HashSet<Guid>();
        
        foreach (var tagId in tagIds)
        {
            var tag = await _tagService.GetTagByIdAsync(tagId);
            if (tag is null)
            {
                return Result<EnvironmentInfo>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
            }
        }

        var environments = (await _fuseStore.GetAsync()).Environments;
        if (environments.Any(e => string.Equals(e.Name,command.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<EnvironmentInfo>.Failure($"Environment with name '{command.Name}' already exists.", ErrorType.Conflict);
        }

        var newEnvironment = new EnvironmentInfo(
            Guid.NewGuid(),
            command.Name,
            command.Description,
            tagIds,
            command.AutoCreateInstances,
            command.BaseUriTemplate,
            command.HealthUriTemplate,
            command.OpenApiUriTemplate
        );

        await _fuseStore.UpdateAsync(store =>
        {
            var updatedEnvironments = store.Environments.Append(newEnvironment).ToList();
            return store with { Environments = updatedEnvironments };
        });

        return Result<EnvironmentInfo>.Success(newEnvironment);
    }

    public async Task<Result> DeleteEnvironmentAsync(DeleteEnvironment command)
    {
        var environment = (await _fuseStore.GetAsync()).Environments
            .FirstOrDefault(e => e.Id == command.Id);

        if (environment is null)
        {
            return Result.Failure($"Environment with ID '{command.Id}' not found.", ErrorType.NotFound);
        }

        await _fuseStore.UpdateAsync(store =>
        {
            var updatedEnvironments = store.Environments
                .Where(e => e.Id != command.Id)
                .ToList();
            return store with { Environments = updatedEnvironments };
        });

        return Result.Success();
    }

    public async Task<IReadOnlyList<EnvironmentInfo>> GetEnvironments()
    {
        return (await _fuseStore.GetAsync()).Environments;
    }

    public async Task<Result<EnvironmentInfo>> UpdateEnvironment(UpdateEnvironment command)
    {
        if (command.Name == string.Empty)
        {
            return Result<EnvironmentInfo>.Failure("Environment name cannot be empty.", ErrorType.Validation);
        }
        
        var tagIds = command.TagIds ?? new HashSet<Guid>();
        
        foreach (var tagId in tagIds)
        {
            var tag = await _tagService.GetTagByIdAsync(tagId);
            if (tag is null)
            {
                return Result<EnvironmentInfo>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
            }
        }

        var environments = (await _fuseStore.GetAsync()).Environments;

        if (environments.Any(e => e.Id != command.Id && string.Equals(e.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<EnvironmentInfo>.Failure($"Environment with name '{command.Name}' already exists.", ErrorType.Conflict);
        }

        var environment = environments
            .FirstOrDefault(e => e.Id == command.Id);

        if (environment is null)
        {
            return Result<EnvironmentInfo>.Failure($"Environment with ID '{command.Id}' not found.", ErrorType.NotFound);
        }

        var updatedEnvironment = environment with
        {
            Name = command.Name,
            Description = command.Description,
            TagIds = tagIds,
            AutoCreateInstances = command.AutoCreateInstances,
            BaseUriTemplate = command.BaseUriTemplate,
            HealthUriTemplate = command.HealthUriTemplate,
            OpenApiUriTemplate = command.OpenApiUriTemplate
        };

        await _fuseStore.UpdateAsync(store =>
        {
            var updatedEnvironments = store.Environments
                .Select(e => e.Id == command.Id ? updatedEnvironment : e)
                .ToList();
            return store with { Environments = updatedEnvironments };
        });

        return Result<EnvironmentInfo>.Success(updatedEnvironment);

    }

    public async Task<Result<int>> ApplyEnvironmentAutomationAsync(ApplyEnvironmentAutomation command)
    {
        var store = await _fuseStore.GetAsync();
        
        // Determine which environments to process
        var environmentsToProcess = command.EnvironmentId.HasValue
            ? store.Environments.Where(e => e.Id == command.EnvironmentId.Value && e.AutoCreateInstances).ToList()
            : store.Environments.Where(e => e.AutoCreateInstances).ToList();

        if (!environmentsToProcess.Any())
        {
            return Result<int>.Success(0);
        }

        // Determine which applications to process
        var applicationsToProcess = command.ApplicationId.HasValue
            ? store.Applications.Where(a => a.Id == command.ApplicationId.Value).ToList()
            : store.Applications.ToList();

        if (!applicationsToProcess.Any())
        {
            return Result<int>.Success(0);
        }

        var instancesCreated = 0;
        var now = DateTime.UtcNow;

        await _fuseStore.UpdateAsync(s =>
        {
            var updatedApplications = new List<Application>();

            foreach (var app in s.Applications)
            {
                // Skip if this app is not in our processing list
                if (!applicationsToProcess.Any(a => a.Id == app.Id))
                {
                    updatedApplications.Add(app);
                    continue;
                }

                var instances = app.Instances.ToList();
                var modified = false;

                foreach (var env in environmentsToProcess)
                {
                    // Check if an instance already exists for this environment
                    var existingInstance = instances.FirstOrDefault(i => i.EnvironmentId == env.Id);
                    
                    if (existingInstance != null)
                    {
                        // Update existing instance if any URIs are empty
                        var needsUpdate = false;
                        var updatedBaseUri = existingInstance.BaseUri;
                        var updatedHealthUri = existingInstance.HealthUri;
                        var updatedOpenApiUri = existingInstance.OpenApiUri;

                        if (existingInstance.BaseUri == null && !string.IsNullOrWhiteSpace(env.BaseUriTemplate))
                        {
                            updatedBaseUri = ApplyTemplate(env.BaseUriTemplate, app.Name, env.Name);
                            needsUpdate = true;
                        }

                        if (existingInstance.HealthUri == null && !string.IsNullOrWhiteSpace(env.HealthUriTemplate))
                        {
                            updatedHealthUri = ApplyTemplate(env.HealthUriTemplate, app.Name, env.Name);
                            needsUpdate = true;
                        }

                        if (existingInstance.OpenApiUri == null && !string.IsNullOrWhiteSpace(env.OpenApiUriTemplate))
                        {
                            updatedOpenApiUri = ApplyTemplate(env.OpenApiUriTemplate, app.Name, env.Name);
                            needsUpdate = true;
                        }

                        if (needsUpdate)
                        {
                            var updatedInstance = existingInstance with
                            {
                                BaseUri = updatedBaseUri,
                                HealthUri = updatedHealthUri,
                                OpenApiUri = updatedOpenApiUri,
                                UpdatedAt = now
                            };

                            var index = instances.FindIndex(i => i.Id == existingInstance.Id);
                            instances[index] = updatedInstance;
                            instancesCreated++;
                            modified = true;
                        }
                    }
                    else
                    {
                        // Create new instance with template-based URIs
                        var newInstance = new ApplicationInstance(
                            Id: Guid.NewGuid(),
                            EnvironmentId: env.Id,
                            PlatformId: null,
                            BaseUri: ApplyTemplate(env.BaseUriTemplate, app.Name, env.Name),
                            HealthUri: ApplyTemplate(env.HealthUriTemplate, app.Name, env.Name),
                            OpenApiUri: ApplyTemplate(env.OpenApiUriTemplate, app.Name, env.Name),
                            Version: null,
                            Dependencies: Array.Empty<ApplicationInstanceDependency>(),
                            TagIds: new HashSet<Guid>(),
                            CreatedAt: now,
                            UpdatedAt: now
                        );

                        instances.Add(newInstance);
                        instancesCreated++;
                        modified = true;
                    }
                }

                if (modified)
                {
                    var updatedApp = app with { Instances = instances, UpdatedAt = now };
                    updatedApplications.Add(updatedApp);
                }
                else
                {
                    updatedApplications.Add(app);
                }
            }

            return s with { Applications = updatedApplications };
        });

        return Result<int>.Success(instancesCreated);
    }

    private static Uri? ApplyTemplate(string? template, string appName, string envName)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var result = template
            .Replace("{appname}", appName, StringComparison.OrdinalIgnoreCase)
            .Replace("{env}", envName, StringComparison.OrdinalIgnoreCase);

        // Try to parse as URI, return null if invalid
        return Uri.TryCreate(result, UriKind.Absolute, out var uri) ? uri : null;
    }
}
