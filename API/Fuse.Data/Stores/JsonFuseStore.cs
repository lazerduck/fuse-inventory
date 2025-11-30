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
                Applications: await ReadApplicationsWithMigrationAsync("applications.json", ct),
                DataStores: await ReadAsync<DataStore>("datastores.json", ct),
                Platforms: await ReadAsync<Platform>("platforms.json", ct),
                ExternalResources: await ReadAsync<ExternalResource>("externalresources.json", ct),
                Accounts: await ReadAccountsWithMigrationAsync("accounts.json", ct),
                Identities: await ReadAsync<Identity>("identities.json", ct),
                Tags: await ReadAsync<Tag>("tags.json", ct),
                Environments: await ReadAsync<EnvironmentInfo>("environments.json", ct),
                KumaIntegrations: await ReadAsync<KumaIntegration>("kumaintegrations.json", ct),
                SecretProviders: await ReadAsync<SecretProvider>("secretproviders.json", ct),
                SqlIntegrations: await ReadAsync<SqlIntegration>("sqlintegrations.json", ct),
                Security: await ReadSecurityAsync("security.json", ct)
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
            await WriteAsync("identities.json", snapshot.Identities, ct);
            await WriteAsync("tags.json", snapshot.Tags, ct);
            await WriteAsync("environments.json", snapshot.Environments, ct);
            await WriteAsync("kumaintegrations.json", snapshot.KumaIntegrations, ct);
            await WriteAsync("secretproviders.json", snapshot.SecretProviders, ct);
            await WriteAsync("sqlintegrations.json", snapshot.SqlIntegrations, ct);
            await WriteAsync("security.json", snapshot.Security, ct);

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

    private async Task<IReadOnlyList<Account>> ReadAccountsWithMigrationAsync(string file, CancellationToken ct)
    {
        var path = Path.Combine(_options.DataDirectory, file);
        if (!File.Exists(path)) return Array.Empty<Account>();

        await using var fs = File.OpenRead(path);
        using var doc = await JsonDocument.ParseAsync(fs, cancellationToken: ct);

        var accounts = new List<Account>();
        
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            // Check if this is a legacy account with secretRef field instead of secretBinding
            var hasLegacySecretRef = element.TryGetProperty("secretRef", out var secretRefProp);
            var hasSecretBinding = element.TryGetProperty("secretBinding", out _);

            Account account;
            
            if (hasLegacySecretRef && !hasSecretBinding)
            {
                // Legacy format: deserialize and migrate
                var legacySecretRef = secretRefProp.GetString() ?? string.Empty;
                
                // Deserialize the account (it will have a default SecretBinding)
                var tempAccount = JsonSerializer.Deserialize<Account>(element.GetRawText(), Json);
                if (tempAccount is null) continue;

                // Create the migrated account with proper SecretBinding
                var binding = new SecretBinding(
                    Kind: SecretBindingKind.PlainReference,
                    PlainReference: legacySecretRef,
                    AzureKeyVault: null
                );

                account = tempAccount with 
                { 
                    SecretBinding = binding,
                    UpdatedAt = DateTime.UtcNow
                };
            }
            else
            {
                // New format: deserialize normally
                account = JsonSerializer.Deserialize<Account>(element.GetRawText(), Json)!;
            }

            accounts.Add(account);
        }

        return accounts;
    }

    private async Task<IReadOnlyList<Application>> ReadApplicationsWithMigrationAsync(string file, CancellationToken ct)
    {
        var path = Path.Combine(_options.DataDirectory, file);
        if (!File.Exists(path)) return Array.Empty<Application>();

        await using var fs = File.OpenRead(path);
        using var doc = await JsonDocument.ParseAsync(fs, cancellationToken: ct);

        var applications = new List<Application>();
        
        foreach (var appElement in doc.RootElement.EnumerateArray())
        {
            var application = JsonSerializer.Deserialize<Application>(appElement.GetRawText(), Json);
            if (application is null) continue;

            // Migration: Dependencies created before authKind was added will have authKind=None (default).
            // If a dependency has an accountId or identityId set but authKind is None, we need to migrate
            // to the appropriate authKind to preserve the intended behavior.
            // Note: authKind=None is valid for dependencies that don't require authentication.
            var needsMigration = false;
            var migratedInstances = new List<ApplicationInstance>();

            foreach (var inst in application.Instances)
            {
                var migratedDeps = new List<ApplicationInstanceDependency>();
                var instNeedsMigration = false;

                foreach (var dep in inst.Dependencies)
                {
                    // Only migrate if authKind is None but a credential is set
                    if (dep.AuthKind == DependencyAuthKind.None)
                    {
                        if (dep.IdentityId is not null)
                        {
                            // Has identityId, migrate to Identity authKind
                            migratedDeps.Add(dep with { AuthKind = DependencyAuthKind.Identity });
                            instNeedsMigration = true;
                        }
                        else if (dep.AccountId is not null)
                        {
                            // Has accountId, migrate to Account authKind
                            migratedDeps.Add(dep with { AuthKind = DependencyAuthKind.Account });
                            instNeedsMigration = true;
                        }
                        else
                        {
                            // No credentials set, keep authKind=None (valid state)
                            migratedDeps.Add(dep);
                        }
                    }
                    else
                    {
                        migratedDeps.Add(dep);
                    }
                }

                if (instNeedsMigration)
                {
                    needsMigration = true;
                    migratedInstances.Add(inst with { Dependencies = migratedDeps, UpdatedAt = DateTime.UtcNow });
                }
                else
                {
                    migratedInstances.Add(inst);
                }
            }

            if (needsMigration)
            {
                applications.Add(application with { Instances = migratedInstances, UpdatedAt = DateTime.UtcNow });
            }
            else
            {
                applications.Add(application);
            }
        }

        return applications;
    }
}
