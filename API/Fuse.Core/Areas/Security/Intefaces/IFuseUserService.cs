using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Interfaces;

public interface IFuseUserService
{
    Task<Result<FuseUser>> GetUser(Guid id);
    // Returns a failure if no user found!
}