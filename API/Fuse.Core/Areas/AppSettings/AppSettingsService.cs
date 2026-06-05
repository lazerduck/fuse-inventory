using Fuse.Core.Interfaces;

namespace Fuse.Core.Areas.AppSettings;

public class AppSettingsService(IFuseStore fuseStore) : IAppSettingsService
{
    public async Task<Models.AppSettings> GetAppSettingsAsync() 
        => await fuseStore.GetAsync(snapshot => snapshot.AppSettings);

    public Task UpdateAppSettingsAsync(Models.AppSettings settings)
    {
        return fuseStore.UpdateAsync(snapshot => snapshot with { AppSettings = settings });
    }
}