using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class ServerService : IServerService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;

    public ServerService(IFuseStore fuseStore, ITagService tagService)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
    }

    public async Task<IReadOnlyList<Server>> GetServersAsync()
        => (await _fuseStore.GetAsync()).Servers;

    public async Task<Server?> GetServerByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).Servers.FirstOrDefault(s => s.Id == id);

    public async Task<Result<Server>> CreateServerAsync(CreateServer command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<Server>.Failure("Server name cannot be empty.", ErrorType.Validation);
        if (string.IsNullOrWhiteSpace(command.Hostname))
            return Result<Server>.Failure("Hostname cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        // Environment must exist
        if (!store.Environments.Any(e => e.Id == command.EnvironmentId))
            return Result<Server>.Failure($"Environment with ID '{command.EnvironmentId}' not found.", ErrorType.Validation);

        // Validate tags
        foreach (var tagId in command.TagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Server>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        // Unique name per environment
        if (store.Servers.Any(s => s.EnvironmentId == command.EnvironmentId && string.Equals(s.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<Server>.Failure($"Server with name '{command.Name}' already exists in this environment.", ErrorType.Conflict);

        var now = DateTime.UtcNow;
        var server = new Server(
            Id: Guid.NewGuid(),
            Name: command.Name,
            Description: null,
            Hostname: command.Hostname,
            OperatingSystem: command.OperatingSystem,
            EnvironmentId: command.EnvironmentId,
            TagIds: command.TagIds,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { Servers = s.Servers.Append(server).ToList() });
        return Result<Server>.Success(server);
    }

    public async Task<Result<Server>> UpdateServerAsync(UpdateServer command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<Server>.Failure("Server name cannot be empty.", ErrorType.Validation);
        if (string.IsNullOrWhiteSpace(command.Hostname))
            return Result<Server>.Failure("Hostname cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        var existing = store.Servers.FirstOrDefault(s => s.Id == command.Id);
        if (existing is null)
            return Result<Server>.Failure($"Server with ID '{command.Id}' not found.", ErrorType.NotFound);

        if (!store.Environments.Any(e => e.Id == command.EnvironmentId))
            return Result<Server>.Failure($"Environment with ID '{command.EnvironmentId}' not found.", ErrorType.Validation);

        foreach (var tagId in command.TagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<Server>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        if (store.Servers.Any(s => s.Id != command.Id && s.EnvironmentId == command.EnvironmentId && string.Equals(s.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<Server>.Failure($"Server with name '{command.Name}' already exists in this environment.", ErrorType.Conflict);

        var updated = existing with
        {
            Name = command.Name,
            Hostname = command.Hostname,
            OperatingSystem = command.OperatingSystem,
            EnvironmentId = command.EnvironmentId,
            TagIds = command.TagIds,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { Servers = s.Servers.Select(x => x.Id == command.Id ? updated : x).ToList() });
        return Result<Server>.Success(updated);
    }

    public async Task<Result> DeleteServerAsync(DeleteServer command)
    {
        var store = await _fuseStore.GetAsync();
        if (!store.Servers.Any(s => s.Id == command.Id))
            return Result.Failure($"Server with ID '{command.Id}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s => s with { Servers = s.Servers.Where(x => x.Id != command.Id).ToList() });
        return Result.Success();
    }
}
