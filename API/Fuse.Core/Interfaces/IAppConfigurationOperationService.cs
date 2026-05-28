using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IAppConfigurationOperationService
{
    Task<Result<IReadOnlyList<AppConfigurationEntry>>> ListKeyValuesAsync(
        Guid providerId,
        string? keySearch = null,
        string? keyPrefix = null,
        string? label = null);
}
