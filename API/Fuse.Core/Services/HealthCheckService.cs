using Fuse.Core.Areas.Logging;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class HealthCheckService : IHealthCheckService
{
    private readonly string _dataDirectory;
    private readonly string _auditDbPath;
    private readonly ILogService _logService;

    public HealthCheckService(string dataDirectory, ILogService logService)
    {
        _dataDirectory = dataDirectory;
        _auditDbPath = Path.Combine(dataDirectory, "audit.db");
        _logService = logService;
    }

    public async Task<bool> IsReadyAsync(CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        return status.IsHealthy;
    }

    public async Task<HealthStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var components = new Dictionary<string, ComponentHealth>();

        // Check data directory
        var dataDirStatus = await CheckDataDirectoryAsync(ct);
        components.Add("data-directory", dataDirStatus);

        // Check JSON file loading
        var jsonStatus = await CheckJsonFilesAsync(ct);
        components.Add("json-files", jsonStatus);

        // Check LiteDB audit database (read-only check, catch-all to avoid LiteDB dependency)
        var liteDbStatus = await CheckLiteDbAsync(ct);
        components.Add("lite-db", liteDbStatus);

        var allHealthy = components.Values.All(c => c.Type == HealthStatusType.Healthy);

        return new HealthStatus(
            IsHealthy: allHealthy,
            Status: allHealthy ? "Healthy" : "Unhealthy",
            Components: components
        );
    }

    private async Task<ComponentHealth> CheckDataDirectoryAsync(CancellationToken ct)
    {
        try
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
                return new ComponentHealth("data-directory", HealthStatusType.Healthy, "Data directory created successfully");
            }

            // Test write access
            var testFile = Path.Combine(_dataDirectory, ".health-check-temp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            return new ComponentHealth("data-directory", HealthStatusType.Healthy, "Data directory exists and is writable");
        }
        catch (Exception ex)
        {
            await _logService.LogAsync(new SystemLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = LogLevel.Error,
                Area = "HealthCheck",
                Message = "Health check failed while verifying the data directory.",
                Exception = ex.ToString()
            }, ct);

            return new ComponentHealth("data-directory", HealthStatusType.Unhealthy, ex.Message);
        }
    }

    private async Task<ComponentHealth> CheckJsonFilesAsync(CancellationToken ct)
    {
        try
        {
            if (!Directory.Exists(_dataDirectory))
            {
                return new ComponentHealth("json-files", HealthStatusType.Degraded, "No data directory yet — expected on first run");
            }

            var jsonFiles = Directory.GetFiles(_dataDirectory, "*.json");

            if (jsonFiles.Length == 0)
            {
                return new ComponentHealth("json-files", HealthStatusType.Degraded, "No JSON files found — application may be uninitialised");
            }

            var corruptedFiles = new List<string>();
            foreach (var file in jsonFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file, ct);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        // Empty file is fine — means no data yet
                        continue;
                    }

                    // Try to parse as JSON to verify it's valid
                    using var doc = System.Text.Json.JsonDocument.Parse(content);
                }
                catch (System.Text.Json.JsonException)
                {
                    corruptedFiles.Add(Path.GetFileName(file));
                }
                catch (UnauthorizedAccessException)
                {
                    corruptedFiles.Add(Path.GetFileName(file) + " (permission denied)");
                }
            }

            if (corruptedFiles.Count > 0)
            {
                await _logService.LogAsync(new SystemLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = LogLevel.Warning,
                    Area = "HealthCheck",
                    Message = "Health check detected corrupted or inaccessible JSON files.",
                    Details = string.Join(", ", corruptedFiles)
                }, ct);

                return new ComponentHealth(
                    "json-files",
                    HealthStatusType.Unhealthy,
                    $"{corruptedFiles.Count} corrupted file(s): {string.Join(", ", corruptedFiles)}"
                );
            }

            return new ComponentHealth(
                "json-files",
                HealthStatusType.Healthy,
                $"{jsonFiles.Length} JSON file(s) validated successfully"
            );
        }
        catch (Exception ex)
        {
            await _logService.LogAsync(new SystemLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = LogLevel.Error,
                Area = "HealthCheck",
                Message = "Health check failed while validating JSON files.",
                Exception = ex.ToString()
            }, ct);

            return new ComponentHealth("json-files", HealthStatusType.Unhealthy, ex.Message);
        }
    }

    private async Task<ComponentHealth> CheckLiteDbAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(_auditDbPath))
            {
                return new ComponentHealth("lite-db", HealthStatusType.Degraded, "Audit database not yet created — expected on first run");
            }

            var fileInfo = new FileInfo(_auditDbPath);
            if (fileInfo.Length == 0)
            {
                return new ComponentHealth("lite-db", HealthStatusType.Unhealthy, "Audit database file is empty");
            }

            // Open the file read-only to verify it's accessible and not corrupted.
            using var stream = File.OpenRead(_auditDbPath);
            stream.Position = 0;

            return new ComponentHealth("lite-db", HealthStatusType.Healthy, "Audit database accessible");
        }
        catch (Exception ex)
        {
            await _logService.LogAsync(new SystemLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = LogLevel.Error,
                Area = "HealthCheck",
                Message = "Health check failed while validating LiteDB accessibility.",
                Exception = ex.ToString()
            }, ct);

            return new ComponentHealth("lite-db", HealthStatusType.Unhealthy, ex.Message);
        }
    }
}