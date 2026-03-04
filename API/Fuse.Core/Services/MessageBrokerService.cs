using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class MessageBrokerService : IMessageBrokerService
{
    private readonly IFuseStore _fuseStore;
    private readonly ITagService _tagService;

    public MessageBrokerService(IFuseStore fuseStore, ITagService tagService)
    {
        _fuseStore = fuseStore;
        _tagService = tagService;
    }

    public async Task<IReadOnlyList<MessageBroker>> GetMessageBrokersAsync()
        => (await _fuseStore.GetAsync()).MessageBrokers;

    public async Task<MessageBroker?> GetMessageBrokerByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).MessageBrokers.FirstOrDefault(m => m.Id == id);

    public async Task<Result<MessageBroker>> CreateMessageBrokerAsync(CreateMessageBroker command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<MessageBroker>.Failure("Message broker name cannot be empty.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(command.Kind))
            return Result<MessageBroker>.Failure("Message broker kind cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();

        if (!store.Environments.Any(e => e.Id == command.EnvironmentId))
            return Result<MessageBroker>.Failure($"Environment with ID '{command.EnvironmentId}' not found.", ErrorType.Validation);

        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<MessageBroker>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        if (store.MessageBrokers.Any(m => string.Equals(m.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<MessageBroker>.Failure($"Message broker with name '{command.Name}' already exists.", ErrorType.Conflict);

        var now = DateTime.UtcNow;
        var broker = new MessageBroker(
            Id: Guid.NewGuid(),
            Name: command.Name,
            Description: command.Description,
            Kind: command.Kind,
            EnvironmentId: command.EnvironmentId,
            ConnectionUri: command.ConnectionUri,
            TagIds: tagIds,
            CreatedAt: now,
            UpdatedAt: now,
            Queues: command.Queues?.Select(q => new BrokerQueue(Guid.NewGuid(), q.Name, q.Description)).ToList(),
            Topics: command.Topics?.Select(t => new BrokerTopic(Guid.NewGuid(), t.Name, t.Description, t.Subscribers ?? new List<string>())).ToList()
        );

        await _fuseStore.UpdateAsync(s => s with { MessageBrokers = s.MessageBrokers.Append(broker).ToList() });
        return Result<MessageBroker>.Success(broker);
    }

    public async Task<Result<MessageBroker>> UpdateMessageBrokerAsync(UpdateMessageBroker command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<MessageBroker>.Failure("Message broker name cannot be empty.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(command.Kind))
            return Result<MessageBroker>.Failure("Message broker kind cannot be empty.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        var existing = store.MessageBrokers.FirstOrDefault(m => m.Id == command.Id);
        if (existing is null)
            return Result<MessageBroker>.Failure($"Message broker with ID '{command.Id}' not found.", ErrorType.NotFound);

        if (!store.Environments.Any(e => e.Id == command.EnvironmentId))
            return Result<MessageBroker>.Failure($"Environment with ID '{command.EnvironmentId}' not found.", ErrorType.Validation);

        var tagIds = command.TagIds ?? new HashSet<Guid>();
        foreach (var tagId in tagIds)
        {
            if (await _tagService.GetTagByIdAsync(tagId) is null)
                return Result<MessageBroker>.Failure($"Tag with ID '{tagId}' not found.", ErrorType.Validation);
        }

        if (store.MessageBrokers.Any(m => m.Id != command.Id && string.Equals(m.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
            return Result<MessageBroker>.Failure($"Message broker with name '{command.Name}' already exists.", ErrorType.Conflict);

        var updated = existing with
        {
            Name = command.Name,
            Description = command.Description,
            Kind = command.Kind,
            EnvironmentId = command.EnvironmentId,
            ConnectionUri = command.ConnectionUri,
            TagIds = tagIds,
            UpdatedAt = DateTime.UtcNow,
            Queues = command.Queues?.Select(q => new BrokerQueue(Guid.NewGuid(), q.Name, q.Description)).ToList(),
            Topics = command.Topics?.Select(t => new BrokerTopic(Guid.NewGuid(), t.Name, t.Description, t.Subscribers ?? new List<string>())).ToList()
        };

        await _fuseStore.UpdateAsync(s => s with { MessageBrokers = s.MessageBrokers.Select(m => m.Id == command.Id ? updated : m).ToList() });
        return Result<MessageBroker>.Success(updated);
    }

    public async Task<Result> DeleteMessageBrokerAsync(DeleteMessageBroker command)
    {
        var store = await _fuseStore.GetAsync();
        if (!store.MessageBrokers.Any(m => m.Id == command.Id))
            return Result.Failure($"Message broker with ID '{command.Id}' not found.", ErrorType.NotFound);

        await _fuseStore.UpdateAsync(s => s with { MessageBrokers = s.MessageBrokers.Where(m => m.Id != command.Id).ToList() });
        return Result.Success();
    }
}
