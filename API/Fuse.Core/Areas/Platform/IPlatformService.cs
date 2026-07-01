using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Platform;

public interface IPlatformService
{
    Task<IReadOnlyList<Models.Platform>> GetPlatformsAsync();
    Task<Models.Platform?> GetPlatformByIdAsync(Guid id);
    Task<Result<Models.Platform>> CreatePlatformAsync(CreatePlatform command);
    Task<Result<Models.Platform>> UpdatePlatformAsync(UpdatePlatform command);
    Task<Result> DeletePlatformAsync(DeletePlatform command);
}
