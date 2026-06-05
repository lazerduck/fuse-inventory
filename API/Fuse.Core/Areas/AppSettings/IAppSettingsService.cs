namespace Fuse.Core.Areas.AppSettings;

using Fuse.Core.Models;

public interface IAppSettingsService
{
    Task<AppSettings> GetAppSettingsAsync();

    Task UpdateAppSettingsAsync(AppSettings settings);
}