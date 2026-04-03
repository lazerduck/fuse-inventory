namespace Fuse.Core.Areas.Security.Services;

using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

public class FuseSecurityService(IFuseStore fuseStore) : IFuseSecurityService
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
}
