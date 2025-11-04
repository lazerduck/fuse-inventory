using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IDataStoreService
{
    Task<IReadOnlyList<DataStore>> GetDataStoresAsync();
    Task<DataStore?> GetDataStoreByIdAsync(Guid id);
    Task<Result<DataStore>> CreateDataStoreAsync(CreateDataStore command);
    Task<Result<DataStore>> UpdateDataStoreAsync(UpdateDataStore command);
    Task<Result> DeleteDataStoreAsync(DeleteDataStore command);
}
