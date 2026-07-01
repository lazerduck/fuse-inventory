using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Risk;

public interface IRiskService
{
    Task<IReadOnlyList<Models.Risk>> GetRisksAsync();
    Task<Models.Risk?> GetRiskByIdAsync(Guid id);
    Task<IReadOnlyList<Models.Risk>> GetRisksByTargetAsync(string targetType, Guid targetId);
    Task<Result<Models.Risk>> CreateRiskAsync(CreateRisk command);
    Task<Result<Models.Risk>> UpdateRiskAsync(UpdateRisk command);
    Task<Result> DeleteRiskAsync(DeleteRisk command);
}
