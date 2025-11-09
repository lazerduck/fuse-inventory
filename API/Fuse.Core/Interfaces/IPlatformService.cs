using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IPlatformService
{
    Task<IReadOnlyList<Platform>> GetPlatformsAsync();
    Task<Platform?> GetPlatformByIdAsync(Guid id);
    Task<Result<Platform>> CreatePlatformAsync(CreatePlatform command);
    Task<Result<Platform>> UpdatePlatformAsync(UpdatePlatform command);
    Task<Result> DeletePlatformAsync(DeletePlatform command);
}
