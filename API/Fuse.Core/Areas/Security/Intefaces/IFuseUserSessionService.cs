using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Interfaces;

public interface IFuseUserSessionService
{
    Task<Result<string>> CreateSession(FuseUser user);

    Task<Result<string>> RefreshSession(string token);

    Task<Result> DeleteSession(string token);
}