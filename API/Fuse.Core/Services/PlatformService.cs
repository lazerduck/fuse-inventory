using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class PlatformService : IPlatformService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;

    public PlatformService(IFuseStore fuseStore, ITagService tagService)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
    }

    public async Task<IReadOnlyList<Platform>> GetPlatformsAsync()
        => (await _fuseStore.GetAsync()).Platforms;

    public async Task<Platform?> GetPlatformByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).Platforms.FirstOrDefault(s => s.Id == id);

    public async Task<Result<Platform>> CreatePlatformAsync(CreatePlatform command)
    {
        if (string.IsNullOrWhiteSpace(command.DisplayName))
            return Result<Platform>.Failure("Platform display name cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();

        // Validate tags
        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Platform>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        // Unique display name globally
        if (store.Platforms.Any(s => string.Equals(s.DisplayName, command.DisplayName, StringComparison.OrdinalIgnoreCase)))
            return Result<Platform>.Failure($"Platform with display name '{command.DisplayName}' already exists.", ErrorType.Conflict);

        var now = DateTime.UtcNow;
        var platform = new Platform(
            Id: Guid.NewGuid(),
            DisplayName: command.DisplayName,
            DnsName: command.DnsName,
            Os: command.Os,
            Kind: command.Kind,
            IpAddress: command.IpAddress,
            Notes: command.Notes,
            TagIds: tagIds,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { Platforms = s.Platforms.Append(platform).ToList() });
        return Result<Platform>.Success(platform);
    }

    public async Task<Result<Platform>> UpdatePlatformAsync(UpdatePlatform command)
    {
        if (string.IsNullOrWhiteSpace(command.DisplayName))
            return Result<Platform>.Failure("Platform display name cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        var existing = store.Platforms.FirstOrDefault(s => s.Id == command.Id);
        if (existing is null)
            return Result<Platform>.Failure($"Platform with ID '{command.Id}' not found.", ErrorType.NotFound);

        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Platform>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        // Unique display name globally (excluding current platform)
        if (store.Platforms.Any(s => s.Id != command.Id && string.Equals(s.DisplayName, command.DisplayName, StringComparison.OrdinalIgnoreCase)))
            return Result<Platform>.Failure($"Platform with display name '{command.DisplayName}' already exists.", ErrorType.Conflict);

        var updated = existing with
        {
            DisplayName = command.DisplayName,
            DnsName = command.DnsName,
            Os = command.Os,
            Kind = command.Kind,
            IpAddress = command.IpAddress,
            Notes = command.Notes,
            TagIds = tagIds,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { Platforms = s.Platforms.Select(x => x.Id == command.Id ? updated : x).ToList() });
        return Result<Platform>.Success(updated);
    }

    public async Task<Result> DeletePlatformAsync(DeletePlatform command)
    {
        var store = await _fuseStore.GetAsync();
        if (!store.Platforms.Any(s => s.Id == command.Id))
            return Result.Failure($"Platform with ID '{command.Id}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s => s with { Platforms = s.Platforms.Where(x => x.Id != command.Id).ToList() });
        return Result.Success();
    }
}
