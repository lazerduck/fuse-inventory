using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class DataStoreService : IDataStoreService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;

    public DataStoreService(IFuseStore fuseStore, ITagService tagService)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
    }

    public async Task<IReadOnlyList<DataStore>> GetDataStoresAsync()
        => (await _fuseStore.GetAsync()).DataStores;

    public async Task<DataStore?> GetDataStoreByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).DataStores.FirstOrDefault(d => d.Id == id);

    public async Task<Result<DataStore>> CreateDataStoreAsync(CreateDataStore command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<DataStore>.Failure("Data store name cannot be empty.", ErrorType.Validation);
        if (string.IsNullOrWhiteSpace(command.Kind))
            return Result<DataStore>.Failure("Data store kind cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        if (!store.Environments.Any(e => e.Id == command.EnvironmentId))
            return Result<DataStore>.Failure($"Environment with ID '{command.EnvironmentId}' not found.", ErrorType.Validation);

        if (command.ServerId is Guid sid)
        {
            var server = store.Servers.FirstOrDefault(s => s.Id == sid);
            if (server is null)
                return Result<DataStore>.Failure($"Server with ID '{sid}' not found.", ErrorType.Validation);
            if (server.EnvironmentId != command.EnvironmentId)
                return Result<DataStore>.Failure("Server and DataStore must belong to the same environment.", ErrorType.Validation);
        }

        foreach (var tagId in command.TagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<DataStore>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        if (store.DataStores.Any(d => d.EnvironmentId == command.EnvironmentId && string.Equals(d.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<DataStore>.Failure($"Data store with name '{command.Name}' already exists in this environment.", ErrorType.Conflict);

        var now = DateTime.UtcNow;
        var ds = new DataStore(
            Id: Guid.NewGuid(),
            Name: command.Name,
            Description: null,
            Kind: command.Kind,
            EnvironmentId: command.EnvironmentId,
            ServerId: command.ServerId,
            ConnectionUri: command.ConnectionUri,
            TagIds: command.TagIds,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { DataStores = s.DataStores.Append(ds).ToList() });
        return Result<DataStore>.Success(ds);
    }

    public async Task<Result<DataStore>> UpdateDataStoreAsync(UpdateDataStore command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<DataStore>.Failure("Data store name cannot be empty.", ErrorType.Validation);
        if (string.IsNullOrWhiteSpace(command.Kind))
            return Result<DataStore>.Failure("Data store kind cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        var existing = store.DataStores.FirstOrDefault(d => d.Id == command.Id);
        if (existing is null)
            return Result<DataStore>.Failure($"Data store with ID '{command.Id}' not found.", ErrorType.NotFound);

        if (!store.Environments.Any(e => e.Id == command.EnvironmentId))
            return Result<DataStore>.Failure($"Environment with ID '{command.EnvironmentId}' not found.", ErrorType.Validation);

        if (command.ServerId is Guid sid)
        {
            var server = store.Servers.FirstOrDefault(s => s.Id == sid);
            if (server is null)
                return Result<DataStore>.Failure($"Server with ID '{sid}' not found.", ErrorType.Validation);
            if (server.EnvironmentId != command.EnvironmentId)
                return Result<DataStore>.Failure("Server and DataStore must belong to the same environment.", ErrorType.Validation);
        }

        foreach (var tagId in command.TagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<DataStore>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        if (store.DataStores.Any(d => d.Id != command.Id && d.EnvironmentId == command.EnvironmentId && string.Equals(d.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<DataStore>.Failure($"Data store with name '{command.Name}' already exists in this environment.", ErrorType.Conflict);

        var updated = existing with
        {
            Name = command.Name,
            Kind = command.Kind,
            EnvironmentId = command.EnvironmentId,
            ServerId = command.ServerId,
            ConnectionUri = command.ConnectionUri,
            TagIds = command.TagIds,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with { DataStores = s.DataStores.Select(x => x.Id == command.Id ? updated : x).ToList() });
        return Result<DataStore>.Success(updated);
    }

    public async Task<Result> DeleteDataStoreAsync(DeleteDataStore command)
    {
        var store = await _fuseStore.GetAsync();
        if (!store.DataStores.Any(d => d.Id == command.Id))
            return Result.Failure($"Data store with ID '{command.Id}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s => s with { DataStores = s.DataStores.Where(x => x.Id != command.Id).ToList() });
        return Result.Success();
    }
}
