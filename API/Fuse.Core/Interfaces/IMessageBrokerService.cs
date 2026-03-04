using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IMessageBrokerService
{
    Task<IReadOnlyList<MessageBroker>> GetMessageBrokersAsync();
    Task<MessageBroker?> GetMessageBrokerByIdAsync(Guid id);
    Task<Result<MessageBroker>> CreateMessageBrokerAsync(CreateMessageBroker command);
    Task<Result<MessageBroker>> UpdateMessageBrokerAsync(UpdateMessageBroker command);
    Task<Result> DeleteMessageBrokerAsync(DeleteMessageBroker command);
}
