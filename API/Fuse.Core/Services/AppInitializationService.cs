using Fuse.Core.Interfaces;

namespace Fuse.Core.Services;

/// <summary>
/// Orchestrates startup by executing all registered <see cref="IStartupTask"/> instances
/// in ascending <see cref="IStartupTask.Order"/>. Adding a new startup concern only
/// requires creating a task class and registering it — no changes here.
/// </summary>
public class AppInitializationService : IAppInitializationService
{
    private readonly IEnumerable<IStartupTask> _tasks;

    public AppInitializationService(IEnumerable<IStartupTask> tasks)
    {
        _tasks = tasks;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        foreach (var task in _tasks.OrderBy(t => t.Order))
            await task.RunAsync(ct);
    }
}
