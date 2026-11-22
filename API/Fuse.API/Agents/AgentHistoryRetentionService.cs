using Fuse.Core.Areas.PlatformAgent;

namespace Fuse.API.Agents;

public sealed class AgentHistoryRetentionService(IAgentReportingStore store, ILogger<AgentHistoryRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try { store.PruneHistory(); }
            catch (Exception ex) { logger.LogError(ex, "Could not prune agent usage history."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
