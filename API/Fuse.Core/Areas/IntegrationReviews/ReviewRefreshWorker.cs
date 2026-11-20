using Fuse.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fuse.Core.Areas.IntegrationReviews;

public sealed class ReviewRefreshWorker(IServiceScopeFactory scopes, ILogger<ReviewRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var snapshot = await scope.ServiceProvider.GetRequiredService<IFuseStore>().GetAsync(stoppingToken);
                var due = snapshot.IntegrationReviews.Where(x => x.RefreshMinutes >= 15 &&
                    (x.LastAttempt is null || x.LastAttempt.CheckedAt.AddMinutes(x.RefreshMinutes) <= DateTimeOffset.UtcNow))
                    .OrderBy(x => x.LastAttempt?.CheckedAt).Take(8);
                var service = scope.ServiceProvider.GetRequiredService<IntegrationReviewService>();
                foreach (var row in due) await service.RefreshAsync(row.Id, stoppingToken);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning("Scheduled integration review failed with {ErrorType}", error.GetType().Name); }
        }
    }
}
