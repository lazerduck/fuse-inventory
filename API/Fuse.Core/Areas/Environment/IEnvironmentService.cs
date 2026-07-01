using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Environment;

public interface IEnvironmentService
{
    Task<IReadOnlyList<EnvironmentInfo>> GetEnvironments();
    Task<Result<EnvironmentInfo>> CreateEnvironment(CreateEnvironment command);
    Task<Result<EnvironmentInfo>> UpdateEnvironment(UpdateEnvironment command);
    Task<Result> DeleteEnvironmentAsync(DeleteEnvironment command);
    Task<Result<int>> ApplyEnvironmentAutomationAsync(ApplyEnvironmentAutomation command);
}
