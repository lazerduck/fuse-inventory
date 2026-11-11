using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.Security.Interfaces;

public interface IEntraSettingsService
{
    Task<EntraSettingsInfo> GetSettings();
    Task<Result> UpdateSettings(UpdateEntraSettings command);
}
