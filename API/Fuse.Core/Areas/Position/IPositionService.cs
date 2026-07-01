using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Position;

public interface IPositionService
{
    Task<IReadOnlyList<Models.Position>> GetPositionsAsync();
    Task<Models.Position?> GetPositionByIdAsync(Guid id);
    Task<Result<Models.Position>> CreatePositionAsync(CreatePosition command);
    Task<Result<Models.Position>> UpdatePositionAsync(UpdatePosition command);
    Task<Result> DeletePositionAsync(DeletePosition command);
}
