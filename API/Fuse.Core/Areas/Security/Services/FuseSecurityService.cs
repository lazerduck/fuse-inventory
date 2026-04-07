namespace Fuse.Core.Areas.Security.Services;

using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Security;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

public class FuseSecurityService(
    IFuseStore fuseStore,
    IEnumerable<AreaPermissions> permissionCatalogs) : IFuseSecurityService
{
    public async Task<SecurityPosture> GetSecurityPosture()
    {
        var snapshot = await fuseStore.GetAsync();
        return snapshot.SecurityContext.Posture;
    }

    public async Task<bool> RequiresSetup()
    {
        var snapshot = await fuseStore.GetAsync();
        return !snapshot.SecurityContext.Users.Any(m => m.IsAdmin);
    }

    public async Task SetSecurityPosture(SecurityPosture posture)
    {
        await fuseStore.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Posture = posture
            }
        });
    }

    public Task<IReadOnlyList<PermissionAreaCatalog>> GetPermissionCatalogs()
    {
        var catalogs = permissionCatalogs
            .Select(catalog => new PermissionAreaCatalog(
                catalog.AreaName,
                catalog.GetPermissions()
                    .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderBy(catalog => catalog.AreaName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<PermissionAreaCatalog>>(catalogs);
    }
}
