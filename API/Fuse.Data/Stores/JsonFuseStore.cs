using System.Text.Json;
using System.Text.Json.Serialization;
using Fuse.Core.Configs;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Data.Stores;

public sealed class JsonFuseStore : IFuseStore
{
    private readonly JsonFuseStoreOptions _options;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private Snapshot? _cache;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonFuseStore(JsonFuseStoreOptions options)
    {
        _options = options;
        Directory.CreateDirectory(_options.DataDirectory);
    }

    public Snapshot? Current => _cache;

    public event Action<Snapshot>? Changed;

    public async Task<Snapshot> GetAsync(CancellationToken ct = default)
        => _cache is not null ? _cache : await LoadAsync(ct);

    public async Task<Snapshot> LoadAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            _cache = new Snapshot(
                Applications: await ReadAsync<Application>("applications.json", ct),
                DataStores: await ReadAsync<DataStore>("datastores.json", ct),
                Platforms: await ReadAsync<Platform>("platforms.json", ct),
                ExternalResources: await ReadAsync<ExternalResource>("externalresources.json", ct),
                Accounts: await ReadAsync<Account>("accounts.json", ct),
                Tags: await ReadAsync<Tag>("tags.json", ct),
                Environments: await ReadAsync<EnvironmentInfo>("environments.json", ct),
                Security: await ReadSecurityAsync("security.json", ct),
                KumaIntegrations: await ReadAsync<KumaIntegration>("kumaintegrations.json", ct)
            );

            // Temporary migration: convert application-target dependencies to instance-target dependencies.
            bool hasChange = false;
            var migratedApps = _cache.Applications
                .Select(app =>
                {
                    bool appChanged = false;
                    var migratedInstances = app.Instances
                        .Select(inst =>
                        {
                            bool instChanged = false;
                            var migratedDeps = new List<ApplicationInstanceDependency>(inst.Dependencies.Count);
                            foreach (var dep in inst.Dependencies)
                            {
                                if (dep.TargetKind == TargetKind.Application)
                                {
                                    // dep.TargetId may be an Application ID – switch to an instance ID
                                    var referencedApp = _cache.Applications.FirstOrDefault(a => a.Id == dep.TargetId);
                                    var targetInstanceId = referencedApp?.Instances
                                        .FirstOrDefault(ii => ii.EnvironmentId == inst.EnvironmentId)?.Id
                                        ?? referencedApp?.Instances.FirstOrDefault()?.Id;

                                    if (targetInstanceId is Guid iid && iid != dep.TargetId)
                                    {
                                        migratedDeps.Add(dep with { TargetId = iid });
                                        instChanged = true;
                                        continue;
                                    }
                                }
                                migratedDeps.Add(dep);
                            }

                            if (instChanged)
                            {
                                appChanged = true;
                                hasChange = true;
                                return inst with { Dependencies = migratedDeps, UpdatedAt = DateTime.UtcNow };
                            }
                            return inst;
                        })
                        .ToList();

                    if (appChanged)
                    {
                        return app with { Instances = migratedInstances, UpdatedAt = DateTime.UtcNow };
                    }
                    return app;
                })
                .ToList();

            if (hasChange)
            {
                _cache = _cache with { Applications = migratedApps };
            }

            var errors = SnapshotValidator.Validate(_cache);
            if (errors.Count > 0)
                throw new InvalidOperationException("Data validation failed:\n" + string.Join("\n", errors));

            return _cache;
        }
        finally { _mutex.Release(); }
    }

    public async Task SaveAsync(Snapshot snapshot, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var errors = SnapshotValidator.Validate(snapshot);
            if (errors.Count > 0)
                throw new InvalidOperationException("Data validation failed:\n" + string.Join("\n", errors));

            await WriteAsync("applications.json", snapshot.Applications, ct);
            await WriteAsync("datastores.json", snapshot.DataStores, ct);
            await WriteAsync("platforms.json", snapshot.Platforms, ct);
            await WriteAsync("externalresources.json", snapshot.ExternalResources, ct);
            await WriteAsync("accounts.json", snapshot.Accounts, ct);
            await WriteAsync("tags.json", snapshot.Tags, ct);
            await WriteAsync("environments.json", snapshot.Environments, ct);
            await WriteAsync("security.json", snapshot.Security, ct);
            await WriteAsync("kumaintegrations.json", snapshot.KumaIntegrations, ct);

            _cache = snapshot; // swap the in-memory snapshot
            Changed?.Invoke(snapshot);
        }
        finally { _mutex.Release(); }
    }

    public async Task UpdateAsync(Func<Snapshot, Snapshot> mutate, CancellationToken ct = default)
    {
        var current = _cache ?? await LoadAsync(ct);
        var next = mutate(current);
        await SaveAsync(next, ct);
    }

    private async Task<IReadOnlyList<T>> ReadAsync<T>(string file, CancellationToken ct)
    {
        var path = Path.Combine(_options.DataDirectory, file);
        if (!File.Exists(path)) return Array.Empty<T>();
        await using var fs = File.OpenRead(path);
        return (await JsonSerializer.DeserializeAsync<IReadOnlyList<T>>(fs, Json, ct)) ?? Array.Empty<T>();
    }

    private async Task WriteAsync<T>(string file, IReadOnlyList<T> value, CancellationToken ct)
    {
        var path = Path.Combine(_options.DataDirectory, file);
        var tmp = path + ".tmp";
        await using (var fs = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(fs, value, Json, ct);
        }
        File.Move(tmp, path, overwrite: true);
    }

    private async Task<SecurityState> ReadSecurityAsync(string file, CancellationToken ct)
    {
        var path = Path.Combine(_options.DataDirectory, file);
        if (!File.Exists(path))
        {
            return new SecurityState(new SecuritySettings(SecurityLevel.None, DateTime.UtcNow), Array.Empty<SecurityUser>());
        }

        await using var fs = File.OpenRead(path);
        return (await JsonSerializer.DeserializeAsync<SecurityState>(fs, Json, ct))
            ?? new SecurityState(new SecuritySettings(SecurityLevel.None, DateTime.UtcNow), Array.Empty<SecurityUser>());
    }

    private async Task WriteAsync<T>(string file, T value, CancellationToken ct)
    {
        var path = Path.Combine(_options.DataDirectory, file);
        var tmp = path + ".tmp";
        await using (var fs = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(fs, value, Json, ct);
        }
        File.Move(tmp, path, overwrite: true);
    }
}
