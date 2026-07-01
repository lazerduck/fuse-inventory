using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.ExternalResource;

public interface IExternalResourceService
{
    Task<IReadOnlyList<Models.ExternalResource>> GetExternalResourcesAsync();
    Task<Models.ExternalResource?> GetExternalResourceByIdAsync(Guid id);
    Task<Result<Models.ExternalResource>> CreateExternalResourceAsync(CreateExternalResource command);
    Task<Result<Models.ExternalResource>> UpdateExternalResourceAsync(UpdateExternalResource command);
    Task<Result> DeleteExternalResourceAsync(DeleteExternalResource command);
}
