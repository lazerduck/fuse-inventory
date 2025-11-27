using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;

namespace Fuse.Tests.TestInfrastructure;

/// <summary>
/// Fake implementation of IEnvironmentService for testing.
/// Uses the real EnvironmentService with a FakeTagService under the hood.
/// </summary>
public sealed class FakeEnvironmentService : IEnvironmentService
{
    private readonly EnvironmentService _realService;

    public FakeEnvironmentService(IFuseStore store)
    {
        _realService = new EnvironmentService(store, new FakeTagService(store));
    }

    public Task<IReadOnlyList<EnvironmentInfo>> GetEnvironments()
        => _realService.GetEnvironments();

    public Task<Result<EnvironmentInfo>> CreateEnvironment(CreateEnvironment command)
        => _realService.CreateEnvironment(command);

    public Task<Result<EnvironmentInfo>> UpdateEnvironment(UpdateEnvironment command)
        => _realService.UpdateEnvironment(command);

    public Task<Result> DeleteEnvironmentAsync(DeleteEnvironment command)
        => _realService.DeleteEnvironmentAsync(command);

    public Task<Result<int>> ApplyEnvironmentAutomationAsync(ApplyEnvironmentAutomation command)
        => _realService.ApplyEnvironmentAutomationAsync(command);
}
