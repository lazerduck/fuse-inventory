using Fuse.API.Controllers;

namespace Fuse.API.Updates;

public sealed class ChannelUpdateService(AboutResponse running, IHttpClientFactory clients, ILogger<ChannelUpdateService> logger)
    : BackgroundService
{
    private volatile AboutResponse _about = running;
    public AboutResponse About => _about;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var build = _about;
        if (build.UpdateStatus == UpdateCheckStatus.Unsupported)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var client = clients.CreateClient(nameof(ChannelUpdateService));
                var version = await PublishedImage.GetAvailableVersionAsync(client, build, stoppingToken);
                _about = build with
                {
                    UpdateStatus = version is null ? UpdateCheckStatus.Current : UpdateCheckStatus.Available,
                    AvailableVersion = version
                };
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not check the {Channel} image for updates", build.Channel);
                _about = _about with { UpdateStatus = UpdateCheckStatus.Unavailable };
            }

            await Task.Delay(_about.UpdateStatus == UpdateCheckStatus.Unavailable
                ? TimeSpan.FromMinutes(15) : TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
