using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.MessageBroker;

public interface IMessageBrokerService
{
    Task<IReadOnlyList<Models.MessageBroker>> GetMessageBrokersAsync();
    Task<Models.MessageBroker?> GetMessageBrokerByIdAsync(Guid id);
    Task<Result<Models.MessageBroker>> CreateMessageBrokerAsync(CreateMessageBroker command);
    Task<Result<Models.MessageBroker>> UpdateMessageBrokerAsync(UpdateMessageBroker command);
    Task<Result> DeleteMessageBrokerAsync(DeleteMessageBroker command);
}
