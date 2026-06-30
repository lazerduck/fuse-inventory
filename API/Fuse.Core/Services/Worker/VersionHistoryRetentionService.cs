using Fuse.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Fuse.Core.Services.Retention;

namespace Fuse.Core.Services.Worker;

/// <summary>Applies the configured version history retention once at startup and daily thereafter.</summary>
public sealed class VersionHistoryRetentionService(
    IVersionHistoryRetentionPolicyService retentionPolicyService,
    ILogger<VersionHistoryRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Apply the configured version history retention policy
                await retentionPolicyService.ApplyRetentionPolicyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Version history retention cleanup failed");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }
}