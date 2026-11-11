using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Interfaces;

public interface IEntraIdentityService
{
    Task<Result<Session>> SignIn(
        VerifiedEntraIdentity identity,
        Guid configurationVersion,
        Guid? linkUserId = null,
        int? linkSessionVersion = null);

    Task<Result> Revoke(Guid userId, bool? disabled = null);
}
