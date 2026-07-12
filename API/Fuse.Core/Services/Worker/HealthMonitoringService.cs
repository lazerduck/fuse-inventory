using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fuse.Core.Areas.HealthMonitoring;
using Fuse.Core.Areas.KumaIntegration;
using Fuse.Core.Areas.Logging;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SystemLogLevel = Fuse.Core.Models.LogLevel;

namespace Fuse.Core.Services.Worker;

public sealed class HealthMonitoringService : BackgroundService, IHealthMonitoringService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private const int MaxBodyBytes = 16 * 1024;
    private readonly IFuseStore _store;
    private readonly IHealthMonitoringStore _healthStore;
    private readonly IKumaHealthService _kuma;
    private readonly ILogService _logService;
    private readonly ILogger<HealthMonitoringService> _logger;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly object _cycleLock = new();
    private CancellationTokenSource? _cycleCts;

    public HealthMonitoringService(IFuseStore store, IHealthMonitoringStore healthStore, IKumaHealthService kuma, ILogService logService, ILogger<HealthMonitoringService> logger)
    {
        _store = store;
        _healthStore = healthStore;
        _kuma = kuma;
        _logService = logService;
        _logger = logger;
        _store.Changed += OnStoreChanged;
    }

    private void OnStoreChanged(Snapshot _)
    {
        lock (_cycleLock) _cycleCts?.Cancel();
        if (_wake.CurrentCount == 0) _wake.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var cycle = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                lock (_cycleLock) _cycleCts = cycle;
                await RunCycleAsync(cycle.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health monitoring cycle failed");
                await _logService.LogAsync(new SystemLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = SystemLogLevel.Error,
                    Area = "HealthMonitoring",
                    Message = "Health monitoring cycle failed.",
                    Exception = ex.ToString()
                }, stoppingToken);
            }
            finally { lock (_cycleLock) _cycleCts = null; }

            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var delay = Task.Delay(Interval, waitCts.Token);
            var wake = _wake.WaitAsync(waitCts.Token);
            await Task.WhenAny(delay, wake);
            await waitCts.CancelAsync();
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        var snapshot = await _store.GetAsync(ct);
        var provider = snapshot.AppSettings.HealthCheckProvider;
        if (provider == HealthCheckProvider.None) return;

        var environments = snapshot.Environments.ToDictionary(x => x.Id, x => x.Name);
        var targets = snapshot.Applications.SelectMany(app => app.Instances
            .Where(instance => instance.HealthUri is not null)
            .Select(instance => (App: app, Instance: instance))).ToList();
        var ids = targets.Select(x => x.Instance.Id).ToHashSet();
        await _healthStore.RemoveOrphansAsync(ids, ct);

        await Parallel.ForEachAsync(targets, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (target, token) =>
        {
            InstanceHealthResult result = provider switch
            {
                HealthCheckProvider.Internal => await CheckInternalAsync(target.App, target.Instance, environments.GetValueOrDefault(target.Instance.EnvironmentId), token),
                HealthCheckProvider.Kuma => CheckKuma(target.App, target.Instance, environments.GetValueOrDefault(target.Instance.EnvironmentId)),
                _ => throw new InvalidOperationException("Unsupported health provider")
            };
            await _healthStore.SaveAsync(result, token);
        });
        await _healthStore.DeleteTransitionsOlderThanAsync(DateTime.UtcNow.AddDays(-7), ct);
    }

    private InstanceHealthResult CheckKuma(Models.Application app, ApplicationInstance instance, string? environment)
    {
        var status = _kuma.GetHealthStatus(instance.HealthUri!.ToString());
        var stale = status is not null && DateTime.UtcNow - status.LastChecked > TimeSpan.FromMinutes(2.5);
        var state = stale ? InstanceHealthState.Unknown : status?.Status switch
        {
            MonitorStatus.Up => InstanceHealthState.Healthy,
            MonitorStatus.Down => InstanceHealthState.Unhealthy,
            _ => InstanceHealthState.Unknown
        };
        return BaseResult(app, instance, environment, HealthCheckProvider.Kuma, state, status?.LastChecked ?? DateTime.UtcNow,
            failure: status is null ? "monitor-not-found" : stale ? "stale" : null, monitor: status?.MonitorName);
    }

    private async Task<InstanceHealthResult> CheckInternalAsync(Models.Application app, ApplicationInstance instance, string? environment, CancellationToken ct)
    {
        var uri = instance.HealthUri!;
        var started = Stopwatch.StartNew();
        try
        {
            var addresses = await HealthUrlPolicy.ResolveAllowedAsync(uri, ct);
            using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, ConnectTimeout = Timeout };
            handler.ConnectCallback = async (context, token) =>
            {
                var address = addresses[Random.Shared.Next(addresses.Length)];
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), token); return new NetworkStream(socket, ownsSocket: true); }
                catch { socket.Dispose(); throw; }
            };
            using var client = new HttpClient(handler) { Timeout = Timeout };
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("FuseInventory-HealthCheck", "1.0"));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var (summary, truncated, redacted) = await ReadSummaryAsync(response, ct);
            if (!response.IsSuccessStatusCode)
            {
                await _logService.LogAsync(new SystemLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = SystemLogLevel.Warning,
                    Area = "HealthMonitoring",
                    Message = $"Health endpoint returned {(int)response.StatusCode} for instance {instance.Id}.",
                    Details = JsonSerializer.Serialize(new
                    {
                        ApplicationId = app.Id,
                        InstanceId = instance.Id,
                        StatusCode = (int)response.StatusCode,
                        Summary = summary,
                        Truncated = truncated,
                        Redacted = redacted
                    })
                }, ct);
            }
            return BaseResult(app, instance, environment, HealthCheckProvider.Internal,
                response.IsSuccessStatusCode ? InstanceHealthState.Healthy : InstanceHealthState.Unhealthy,
                DateTime.UtcNow, started.ElapsedMilliseconds, (int)response.StatusCode,
                response.IsSuccessStatusCode ? null : "http-status", summary, truncated, redacted);
        }
        catch (HealthUrlPolicyException ex)
        {
            await LogHealthFailureAsync(app, instance, "Health URL policy rejected a monitored endpoint.", ex, ct, ex.Category);
            return BaseResult(app, instance, environment, HealthCheckProvider.Internal, InstanceHealthState.Unhealthy, DateTime.UtcNow, started.ElapsedMilliseconds, failure: ex.Category);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await _logService.LogAsync(new SystemLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = SystemLogLevel.Warning,
                Area = "HealthMonitoring",
                Message = $"Health check timed out for instance {instance.Id}.",
                Details = JsonSerializer.Serialize(new
                {
                    ApplicationId = app.Id,
                    InstanceId = instance.Id
                })
            }, ct);

            return BaseResult(app, instance, environment, HealthCheckProvider.Internal, InstanceHealthState.Unhealthy, DateTime.UtcNow, started.ElapsedMilliseconds, failure: "timeout");
        }
        catch (HttpRequestException ex)
        {
            await LogHealthFailureAsync(app, instance, "Health check request failed.", ex, ct, Categorize(ex));
            return BaseResult(app, instance, environment, HealthCheckProvider.Internal, InstanceHealthState.Unhealthy, DateTime.UtcNow, started.ElapsedMilliseconds, failure: Categorize(ex));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for instance {InstanceId}", instance.Id);
            await LogHealthFailureAsync(app, instance, "Health check failed unexpectedly.", ex, ct, "request-failed");
            return BaseResult(app, instance, environment, HealthCheckProvider.Internal, InstanceHealthState.Unhealthy, DateTime.UtcNow, started.ElapsedMilliseconds, failure: "request-failed");
        }
    }

    private Task LogHealthFailureAsync(Models.Application app, ApplicationInstance instance, string message, Exception ex, CancellationToken ct, string category) =>
        _logService.LogAsync(new SystemLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = SystemLogLevel.Warning,
            Area = "HealthMonitoring",
            Message = message,
            Details = JsonSerializer.Serialize(new
            {
                ApplicationId = app.Id,
                InstanceId = instance.Id,
                FailureCategory = category
            }),
            Exception = ex.ToString()
        }, ct);

    private static InstanceHealthResult BaseResult(Models.Application app, ApplicationInstance instance, string? env, HealthCheckProvider provider, InstanceHealthState state,
        DateTime checkedAt, long? duration = null, int? code = null, string? failure = null, string? summary = null, bool truncated = false, bool redacted = false, string? monitor = null) =>
        new(instance.Id, app.Id, app.Name, instance.EnvironmentId, env, instance.HealthUri!.ToString(), provider, state, checkedAt, duration, code, failure, summary, monitor, truncated, redacted);

    private static async Task<(string? Summary, bool Truncated, bool Redacted)> ReadSummaryAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var media = response.Content.Headers.ContentType?.MediaType;
        if (media is null || !(media.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || media.Contains("json", StringComparison.OrdinalIgnoreCase) || media.Contains("xml", StringComparison.OrdinalIgnoreCase)))
            return (null, false, false);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[MaxBodyBytes + 1];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), ct);
            if (count == 0) break;
            read += count;
        }
        var truncated = read > MaxBodyBytes;
        var text = Encoding.UTF8.GetString(buffer, 0, Math.Min(read, MaxBodyBytes));
        var sanitized = Redact(text, out var redacted);
        return (sanitized, truncated, redacted);
    }

    private static string Redact(string value, out bool redacted)
    {
        var result = Regex.Replace(value,
            "(?i)(\\\"?(?:password|secret|token|api[_-]?key|authorization|cookie)\\\"?\\s*[:=]\\s*)(\\\"[^\\\"]*\\\"|[^,&}\\s]+)",
            "$1\\\"[REDACTED]\\\"");
        result = Regex.Replace(result, "(?i)Bearer\\s+[A-Za-z0-9._~+/-]+=*", "Bearer [REDACTED]");
        redacted = result != value;
        return result;
    }

    private static string Categorize(HttpRequestException ex) => ex.HttpRequestError switch
    {
        HttpRequestError.NameResolutionError => "dns",
        HttpRequestError.SecureConnectionError => "tls",
        HttpRequestError.ConnectionError => "connection",
        _ => "http-request"
    };

    public async Task<HealthOverview> GetOverviewAsync(CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);
        var provider = snapshot.AppSettings.HealthCheckProvider;
        var available = provider != HealthCheckProvider.Kuma || snapshot.KumaIntegrations.Count > 0;
        var results = provider == HealthCheckProvider.None ? [] : await _healthStore.GetCurrentAsync(ct);
        return new HealthOverview(provider, available, available ? null : "No Uptime Kuma integration is configured.",
            results.Count(x => x.State == InstanceHealthState.Healthy), results.Count(x => x.State == InstanceHealthState.Unhealthy),
            results.Count(x => x.State == InstanceHealthState.Unknown), results);
    }

    public Task<IReadOnlyList<InstanceHealthTransition>> GetHistoryAsync(Guid instanceId, CancellationToken ct = default) =>
        _healthStore.GetHistoryAsync(instanceId, DateTime.UtcNow.AddDays(-7), ct);

    public override void Dispose() { _store.Changed -= OnStoreChanged; _wake.Dispose(); base.Dispose(); }
}

internal sealed class HealthUrlPolicyException(string category) : Exception(category) { public string Category { get; } = category; }

internal static class HealthUrlPolicy
{
    public static async Task<IPAddress[]> ResolveAllowedAsync(Uri uri, CancellationToken ct)
    {
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) throw new HealthUrlPolicyException("invalid-scheme");
        if (!string.IsNullOrEmpty(uri.UserInfo)) throw new HealthUrlPolicyException("embedded-credentials");
        IPAddress[] addresses;
        try { addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, ct); }
        catch { throw new HealthUrlPolicyException("dns"); }
        if (addresses.Length == 0 || addresses.Any(IsBlocked)) throw new HealthUrlPolicyException("blocked-address");
        return addresses;
    }

    private static bool IsBlocked(IPAddress input)
    {
        var ip = input.IsIPv4MappedToIPv6 ? input.MapToIPv4() : input;
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.IPv6None)) return true;
        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
            return b[0] == 0 || b[0] >= 224 || b[0] == 127 ||
                   (b[0] == 169 && b[1] == 254) ||
                   (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ||
                   (b[0] == 192 && b[1] == 0 && b[2] == 0) ||
                   (b[0] == 192 && b[1] == 0 && b[2] == 2) ||
                   (b[0] == 192 && b[1] == 88 && b[2] == 99) ||
                   (b[0] == 198 && (b[1] == 18 || b[1] == 19 || b[1] == 51)) ||
                   (b[0] == 203 && b[1] == 0 && b[2] == 113);
        return ip.IsIPv6LinkLocal || ip.IsIPv6Multicast || ip.IsIPv6SiteLocal ||
               (b.Length == 16 && b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8);
    }
}
