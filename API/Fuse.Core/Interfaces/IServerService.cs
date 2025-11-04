using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IServerService
{
    Task<IReadOnlyList<Server>> GetServersAsync();
    Task<Server?> GetServerByIdAsync(Guid id);
    Task<Result<Server>> CreateServerAsync(CreateServer command);
    Task<Result<Server>> UpdateServerAsync(UpdateServer command);
    Task<Result> DeleteServerAsync(DeleteServer command);
}
