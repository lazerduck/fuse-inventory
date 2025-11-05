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
            tagIds
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
            TagIds = tagIds
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
}
