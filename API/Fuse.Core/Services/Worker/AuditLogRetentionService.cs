using Fuse.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fuse.Core.Services.Worker;

/// <summary>Applies the configured audit-log retention once at startup and daily thereafter.</summary>
public sealed class AuditLogRetentionService(
    IAuditService auditService,
    ILogger<AuditLogRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await auditService.CleanupOldAuditLogsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Audit log retention cleanup failed");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }
}
