using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Interfaces;

public interface IAuthenticationService
{
    Result<IReadOnlyList<FuseRole>> GetAPIKeyRoles(string key);

    Result<IReadOnlyList<FuseRole>> GetUserRoles(string Token);
}