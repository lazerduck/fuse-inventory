using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IExternalResourceService
{
    Task<IReadOnlyList<ExternalResource>> GetExternalResourcesAsync();
    Task<ExternalResource?> GetExternalResourceByIdAsync(Guid id);
    Task<Result<ExternalResource>> CreateExternalResourceAsync(CreateExternalResource command);
    Task<Result<ExternalResource>> UpdateExternalResourceAsync(UpdateExternalResource command);
    Task<Result> DeleteExternalResourceAsync(DeleteExternalResource command);
}
