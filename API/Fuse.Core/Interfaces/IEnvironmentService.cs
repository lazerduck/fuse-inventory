using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IEnvironmentService
{
    Task<IReadOnlyList<EnvironmentInfo>> GetEnvironments();
    Task<Result<EnvironmentInfo>> CreateEnvironment(CreateEnvironment command);
    Task<Result<EnvironmentInfo>> UpdateEnvironment(UpdateEnvironment command);
    Task<Result> DeleteEnvironmentAsync(DeleteEnvironment command);
}