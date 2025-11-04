using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class TagService : ITagService
{
    private readonly IFuseStore _fuseStore;

    public TagService(IFuseStore fuseStore)
    {
        _fuseStore = fuseStore;
    }

    public async Task<Result<Tag>> CreateTagAsync(CreateTag command)
    {
        if (command.Name == string.Empty)
        {
            return Result<Tag>.Failure("Tag name cannot be empty.", ErrorType.Validation);
        }

        var tags = (await _fuseStore.GetAsync()).Tags;
        if (tags.Any(t => string.Equals(t.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<Tag>.Failure($"Tag with name '{command.Name}' already exists.", ErrorType.Conflict);
        }

        var newTag = new Tag(
            Guid.NewGuid(),
            command.Name,
            command.Description,
            command.Color
        );

        await _fuseStore.UpdateAsync(store =>
        {
            var updatedTags = store.Tags.Append(newTag).ToList();
            return store with { Tags = updatedTags };
        });

        return Result<Tag>.Success(newTag);
    }

    public async Task<Result> DeleteTagAsync(DeleteTag command)
    {
        if (await GetTagByIdAsync(command.Id) is null)
        {
            return Result.Failure($"Tag with ID '{command.Id}' not found.", ErrorType.NotFound);
        }

        await _fuseStore.UpdateAsync(store =>
        {
            var updatedTags = store.Tags
                .Where(t => t.Id != command.Id)
                .ToList();
            return store with { Tags = updatedTags };
        });

        // ToDo: Remove tag references from other entities
        
        return Result.Success();
    }

    public async Task<Tag?> GetTagByIdAsync(Guid id)
    {
        return (await _fuseStore.GetAsync()).Tags
            .FirstOrDefault(t => t.Id == id);
    }

    public async Task<IReadOnlyList<Tag>> GetTagsAsync()
    {
        return (await _fuseStore.GetAsync()).Tags;
    }

    public async Task<Result<Tag>> UpdateTagAsync(UpdateTag command)
    {
        if (command.Name == string.Empty)
        {
            return Result<Tag>.Failure("Tag name cannot be empty.", ErrorType.Validation);
        }

        var tags = (await _fuseStore.GetAsync()).Tags;
        var existingTag = tags.FirstOrDefault(t => t.Id == command.Id);
        if (existingTag is null)
        {
            return Result<Tag>.Failure($"Tag with ID '{command.Id}' not found.", ErrorType.NotFound);
        }

        if (tags.Any(t => t.Id != command.Id && string.Equals(t.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<Tag>.Failure($"Tag with name '{command.Name}' already exists.", ErrorType.Conflict);
        }

        var updatedTag = existingTag with
        {
            Name = command.Name,
            Description = command.Description,
            Color = command.Color
        };

        await _fuseStore.UpdateAsync(store =>
        {
            var updatedTags = store.Tags
                .Select(t => t.Id == command.Id ? updatedTag : t)
                .ToList();
            return store with { Tags = updatedTags };
        });

        return Result<Tag>.Success(updatedTag);
    }
}