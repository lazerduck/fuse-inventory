using Fuse.Core.Interfaces;

namespace Fuse.Core.Services;

public class HealthCheckService : IHealthCheckService
{
    private readonly string _dataDirectory;
    private readonly string _auditDbPath;

    public HealthCheckService(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
        _auditDbPath = Path.Combine(dataDirectory, "audit.db");
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
        var dataDirStatus = CheckDataDirectory();
        components.Add("data-directory", dataDirStatus);

        // Check JSON file loading
        var jsonStatus = await CheckJsonFilesAsync(ct);
        components.Add("json-files", jsonStatus);

        // Check LiteDB audit database (read-only check, catch-all to avoid LiteDB dependency)
        var liteDbStatus = CheckLiteDb();
        components.Add("lite-db", liteDbStatus);

        var allHealthy = components.Values.All(c => c.Type == HealthStatusType.Healthy);

        return new HealthStatus(
            IsHealthy: allHealthy,
            Status: allHealthy ? "Healthy" : "Unhealthy",
            Components: components
        );
    }

    private ComponentHealth CheckDataDirectory()
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
            return new ComponentHealth("json-files", HealthStatusType.Unhealthy, ex.Message);
        }
    }

    private ComponentHealth CheckLiteDb()
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
            return new ComponentHealth("lite-db", HealthStatusType.Unhealthy, ex.Message);
        }
    }
}