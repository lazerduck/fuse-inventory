namespace Fuse.Core.Areas.Security.Interfaces;

using System.Threading.Tasks;
using Fuse.Core.Models;

public interface IFuseSecurityService
{
    Task<SecurityPosture> GetSecurityPosture();

    Task SetSecurityPosture(SecurityPosture posture);

    Task<bool> RequiresSetup();

    Task<IReadOnlyList<PermissionAreaCatalog>> GetPermissionCatalogs();
}