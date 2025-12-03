using System.Text.RegularExpressions;
using Fuse.Core.Interfaces;
using Fuse.Core.Responses;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Fuse.Core.Services;

public class KumaMetricsService : BackgroundService, IKumaHealthService
{
    private readonly ILogger<KumaMetricsService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFuseStore _store;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _ttl;

    public KumaMetricsService(
        ILogger<KumaMetricsService> logger,
        IHttpClientFactory httpClientFactory,
        IFuseStore store,
        IMemoryCache cache)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _store = store;
        _cache = cache;
        _ttl = TimeSpan.FromTicks(_updateInterval.Ticks * 2);
    }

    public HealthStatusResponse? GetHealthStatus(string monitorUrl)
    {
        if (string.IsNullOrWhiteSpace(monitorUrl))
            return null;

        // Normalize URL for comparison (remove trailing slashes, etc.)
        var normalizedUrl = NormalizeUrl(monitorUrl);
        _cache.TryGetValue(GetHealthKey(normalizedUrl), out HealthStatusResponse? status);
        return status;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Kuma Metrics Service starting...");

        // Wait a bit on startup to let other services initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateMetricsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Kuma metrics");
            }

            await Task.Delay(_updateInterval, stoppingToken);
        }

        _logger.LogInformation("Kuma Metrics Service stopping.");
    }

    private async Task UpdateMetricsAsync(CancellationToken ct)
    {
        var snapshot = await _store.GetAsync(ct);
        var integrations = snapshot.KumaIntegrations;

        if (!integrations.Any())
        {
            _logger.LogDebug("No Kuma integrations configured");
            return;
        }

        _logger.LogDebug("Updating metrics for {Count} Kuma integration(s)", integrations.Count);

        foreach (var integration in integrations)
        {
            try
            {
                await FetchMetricsForIntegrationAsync(integration.Uri, integration.ApiKey, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch metrics from {Uri}", integration.Uri);
            }
        }
    }

    private async Task FetchMetricsForIntegrationAsync(Uri baseUri, string apiKey, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("kuma-metrics");
        
        try
        {
            var metricsUrl = new Uri(baseUri, "/metrics");
            var request = new HttpRequestMessage(HttpMethod.Get, metricsUrl);
            
            // Add HTTP Basic Auth if API key is provided
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var authValue = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{apiKey}"));
                request.Headers.Add("Authorization", $"Basic {authValue}");
            }

            using var response = await client.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch metrics from {Uri}: {StatusCode}", metricsUrl, response.StatusCode);
                return;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            ParseAndCacheMetrics(content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception fetching metrics from {Uri}", baseUri);
        }
    }

    private void ParseAndCacheMetrics(string metricsContent)
    {
        // Parse Prometheus-format metrics
        // Example line: monitor_status{monitor_id="1",monitor_name="fuse",monitor_type="http",monitor_url="http://fuse.ubuntu.lan",monitor_hostname="null",monitor_port="null"} 1
        
        // Use a more flexible regex that captures all key-value pairs, then extract what we need
        var lineRegex = new Regex(@"monitor_status\{([^}]+)\}\s+(\d+)", RegexOptions.Multiline);
        var now = DateTime.UtcNow;

        foreach (Match lineMatch in lineRegex.Matches(metricsContent))
        {
            if (lineMatch.Groups.Count >= 3)
            {
                var labels = lineMatch.Groups[1].Value;
                var statusValue = int.Parse(lineMatch.Groups[2].Value);

                // Extract monitor_url and monitor_name from labels
                var urlMatch = Regex.Match(labels, @"monitor_url=""([^""]+)""");
                var nameMatch = Regex.Match(labels, @"monitor_name=""([^""]+)""");

                if (!urlMatch.Success || !nameMatch.Success)
                    continue;

                var monitorUrl = urlMatch.Groups[1].Value;
                var monitorName = nameMatch.Groups[1].Value;
                var normalizedUrl = NormalizeUrl(monitorUrl);
                
                var status = statusValue switch
                {
                    1 => MonitorStatus.Up,
                    0 => MonitorStatus.Down,
                    2 => MonitorStatus.Pending,
                    3 => MonitorStatus.Maintenance,
                    _ => MonitorStatus.Down
                };

                var healthStatus = new HealthStatusResponse(
                    MonitorUrl: normalizedUrl,
                    Status: status,
                    MonitorName: monitorName,
                    LastChecked: now
                );

                _cache.Set(GetHealthKey(normalizedUrl), healthStatus, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _ttl,
                    Priority = CacheItemPriority.Normal
                });
                
                _logger.LogDebug("Cached health status for {Url}: {Status}", normalizedUrl, status);
            }
        }
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        // Remove trailing slashes and normalize to lowercase for consistent comparison
        return url.TrimEnd('/').ToLowerInvariant();
    }

    private static string GetHealthKey(string normalizedUrl) => $"kuma:health:{normalizedUrl}";
}
