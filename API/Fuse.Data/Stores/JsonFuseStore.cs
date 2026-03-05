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
                Identities: await ReadAsync<Identity>("identities.json", ct),
                Tags: await ReadAsync<Tag>("tags.json", ct),
                Environments: await ReadAsync<EnvironmentInfo>("environments.json", ct),
                KumaIntegrations: await ReadAsync<KumaIntegration>("kumaintegrations.json", ct),
                SecretProviders: await ReadAsync<SecretProvider>("secretproviders.json", ct),
                SqlIntegrations: await ReadAsync<SqlIntegration>("sqlintegrations.json", ct),
                Positions: await ReadAsync<Position>("positions.json", ct),
                ResponsibilityTypes: await ReadAsync<ResponsibilityType>("responsibilitytypes.json", ct),
                ResponsibilityAssignments: await ReadAsync<ResponsibilityAssignment>("responsibilityassignments.json", ct),
                Risks: await ReadAsync<Risk>("risks.json", ct),
                MessageBrokers: await ReadAsync<MessageBroker>("messagebrokers.json", ct),
                Security: await ReadSecurityAsync("security.json", ct),
                PasswordGeneratorConfig: await ReadObjectAsync<PasswordGeneratorConfig>("passwordgeneratorconfig.json", ct)
            );

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

            var writeTasks = new List<Task>();

            if(_cache is null || !ReferenceEquals(_cache.Applications, snapshot.Applications))
                writeTasks.Add(WriteAsync("applications.json", snapshot.Applications, ct));
            if(_cache is null || !ReferenceEquals(_cache.DataStores, snapshot.DataStores))
                writeTasks.Add(WriteAsync("datastores.json", snapshot.DataStores, ct));
            if(_cache is null || !ReferenceEquals(_cache.Platforms, snapshot.Platforms))
                writeTasks.Add(WriteAsync("platforms.json", snapshot.Platforms, ct));
            if(_cache is null || !ReferenceEquals(_cache.ExternalResources, snapshot.ExternalResources))
                writeTasks.Add(WriteAsync("externalresources.json", snapshot.ExternalResources, ct));
            if(_cache is null || !ReferenceEquals(_cache.Accounts, snapshot.Accounts))
                writeTasks.Add(WriteAsync("accounts.json", snapshot.Accounts, ct));
            if(_cache is null || !ReferenceEquals(_cache.Identities, snapshot.Identities))
                writeTasks.Add(WriteAsync("identities.json", snapshot.Identities, ct));
            if(_cache is null || !ReferenceEquals(_cache.Tags, snapshot.Tags))
                writeTasks.Add(WriteAsync("tags.json", snapshot.Tags, ct));
            if(_cache is null || !ReferenceEquals(_cache.Environments, snapshot.Environments))
                writeTasks.Add(WriteAsync("environments.json", snapshot.Environments, ct));
            if(_cache is null || !ReferenceEquals(_cache.KumaIntegrations, snapshot.KumaIntegrations))
                writeTasks.Add(WriteAsync("kumaintegrations.json", snapshot.KumaIntegrations, ct));
            if(_cache is null || !ReferenceEquals(_cache.SecretProviders, snapshot.SecretProviders))
                writeTasks.Add(WriteAsync("secretproviders.json", snapshot.SecretProviders, ct));
            if(_cache is null || !ReferenceEquals(_cache.SqlIntegrations, snapshot.SqlIntegrations))
                writeTasks.Add(WriteAsync("sqlintegrations.json", snapshot.SqlIntegrations, ct));
            if(_cache is null || !ReferenceEquals(_cache.Positions, snapshot.Positions))
                writeTasks.Add(WriteAsync("positions.json", snapshot.Positions, ct));
            if(_cache is null || !ReferenceEquals(_cache.ResponsibilityTypes, snapshot.ResponsibilityTypes))
                writeTasks.Add(WriteAsync("responsibilitytypes.json", snapshot.ResponsibilityTypes, ct));
            if(_cache is null || !ReferenceEquals(_cache.ResponsibilityAssignments, snapshot.ResponsibilityAssignments))
                writeTasks.Add(WriteAsync("responsibilityassignments.json", snapshot.ResponsibilityAssignments, ct));
            if(_cache is null || !ReferenceEquals(_cache.Risks, snapshot.Risks))
                writeTasks.Add(WriteAsync("risks.json", snapshot.Risks, ct));
            if(_cache is null || !ReferenceEquals(_cache.MessageBrokers, snapshot.MessageBrokers))
                writeTasks.Add(WriteAsync("messagebrokers.json", snapshot.MessageBrokers, ct));
            if(_cache is null || !ReferenceEquals(_cache.Security, snapshot.Security))
                writeTasks.Add(WriteAsync("security.json", snapshot.Security, ct));
            if(snapshot.PasswordGeneratorConfig is not null && (_cache is null || !ReferenceEquals(_cache.PasswordGeneratorConfig, snapshot.PasswordGeneratorConfig)))
                writeTasks.Add(WriteAsync("passwordgeneratorconfig.json", snapshot.PasswordGeneratorConfig, ct));

            if(writeTasks.Count > 0)
                await Task.WhenAll(writeTasks);

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

    private async Task<T?> ReadObjectAsync<T>(string file, CancellationToken ct) where T : class
    {
        var path = Path.Combine(_options.DataDirectory, file);
        if (!File.Exists(path)) return null;
        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(fs, Json, ct);
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
