using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IRiskService
{
    Task<IReadOnlyList<Risk>> GetRisksAsync();
    Task<Risk?> GetRiskByIdAsync(Guid id);
    Task<IReadOnlyList<Risk>> GetRisksByTargetAsync(string targetType, Guid targetId);
    Task<Result<Risk>> CreateRiskAsync(CreateRisk command);
    Task<Result<Risk>> UpdateRiskAsync(UpdateRisk command);
    Task<Result> DeleteRiskAsync(DeleteRisk command);
}
