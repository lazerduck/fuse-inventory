using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Interfaces;

public interface IFuseRoleService
{
	Task<Result<IReadOnlyList<FuseRole>>> GetRolesByIds(IReadOnlyList<Guid> roleIds);
}
