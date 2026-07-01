using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.DataStore;

public interface IDataStoreService
{
    Task<IReadOnlyList<Models.DataStore>> GetDataStoresAsync();
    Task<Models.DataStore?> GetDataStoreByIdAsync(Guid id);
    Task<Result<Models.DataStore>> CreateDataStoreAsync(CreateDataStore command);
    Task<Result<Models.DataStore>> UpdateDataStoreAsync(UpdateDataStore command);
    Task<Result> DeleteDataStoreAsync(DeleteDataStore command);
}
