using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IPositionService
{
    Task<IReadOnlyList<Position>> GetPositionsAsync();
    Task<Position?> GetPositionByIdAsync(Guid id);
    Task<Result<Position>> CreatePositionAsync(CreatePosition command);
    Task<Result<Position>> UpdatePositionAsync(UpdatePosition command);
    Task<Result> DeletePositionAsync(DeletePosition command);
}
