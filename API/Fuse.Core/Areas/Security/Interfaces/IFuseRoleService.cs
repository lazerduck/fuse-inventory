using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Interfaces;

public interface IFuseRoleService
{
	Task<Result<IReadOnlyList<FuseRole>>> GetRoles();

	Task<Result<FuseRole>> GetRole(Guid id);

	Task<Result<IReadOnlyList<FuseRole>>> GetRolesByIds(IReadOnlyList<Guid> roleIds);

	Task<Result<FuseRole>> CreateRole(string name, string description, IReadOnlyList<string> permissions);

	Task<Result<FuseRole>> UpdateRole(Guid id, string name, string description, IReadOnlyList<string> permissions);

	Task<Result> DeleteRole(Guid id);

	Task<Result<IReadOnlyList<PermissionAreaCatalog>>> GetAvailablePermissions();
}
