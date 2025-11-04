using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class ExternalResourceService : IExternalResourceService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;

    public ExternalResourceService(IFuseStore fuseStore, ITagService tagService)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
    }

    public async Task<IReadOnlyList<ExternalResource>> GetExternalResourcesAsync()
        => (await _fuseStore.GetAsync()).ExternalResources;

    public async Task<ExternalResource?> GetExternalResourceByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).ExternalResources.FirstOrDefault(r => r.Id == id);

    public async Task<Result<ExternalResource>> CreateExternalResourceAsync(CreateExternalResource command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<ExternalResource>.Failure("Resource name cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        foreach (var tagId in command.TagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<ExternalResource>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        if (store.ExternalResources.Any(r => string.Equals(r.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<ExternalResource>.Failure($"External resource with name '{command.Name}' already exists.", ErrorType.Conflict);

        var now = DateTime.UtcNow;
        var res = new ExternalResource(
            Id: Guid.NewGuid(),
            Name: command.Name,
            Description: command.Description,
            ResourceUri: command.ResourceUri,
            TagIds: command.TagIds,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { ExternalResources = s.ExternalResources.Append(res).ToList() });
        return Result<ExternalResource>.Success(res);
    }

    public async Task<Result<ExternalResource>> UpdateExternalResourceAsync(UpdateExternalResource command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<ExternalResource>.Failure("Resource name cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        var existing = store.ExternalResources.FirstOrDefault(r => r.Id == command.Id);
        if (existing is null)
            return Result<ExternalResource>.Failure($"External resource with ID '{command.Id}' not found.", ErrorType.NotFound);

        foreach (var tagId in command.TagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<ExternalResource>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        if (store.ExternalResources.Any(r => r.Id != command.Id && string.Equals(r.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<ExternalResource>.Failure($"External resource with name '{command.Name}' already exists.", ErrorType.Conflict);

        var updated = existing with
        {
            Name = command.Name,
            Description = command.Description,
            ResourceUri = command.ResourceUri,
            TagIds = command.TagIds,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { ExternalResources = s.ExternalResources.Select(x => x.Id == command.Id ? updated : x).ToList() });
        return Result<ExternalResource>.Success(updated);
    }

    public async Task<Result> DeleteExternalResourceAsync(DeleteExternalResource command)
    {
        var store = await _fuseStore.GetAsync();
        if (!store.ExternalResources.Any(r => r.Id == command.Id))
            return Result.Failure($"External resource with ID '{command.Id}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s => s with { ExternalResources = s.ExternalResources.Where(x => x.Id != command.Id).ToList() });
        return Result.Success();
    }
}
