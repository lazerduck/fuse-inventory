using Fuse.Core.Models;
using Fuse.Core.Interfaces;
using Fuse.Data.Stores;
using Xunit;

namespace Fuse.Tests.Services;

public class VersionHistoryRetentionTests
{
    private static readonly SemaphoreSlim CreateLock = new(1, 1);

    private static async Task<string> CreateTempDir()
    {
        await CreateLock.WaitAsync();
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), $"fuse-vh-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(dir);
            return dir;
        }
        finally
        {
            CreateLock.Release();
        }
    }

    [Fact]
    public async Task SaveVersion_UnlimitedKeepsAllVersions()
    {
        var settings = new AppSettings(
            IncompleteDataWarningEnabled: true,
            LocalLicenseValidationOnly: false,
            HideValidLicenseChip: false,
            VersionHistoryKeepCount: 0
        );
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbVersionHistoryService(store, dir);

        var entityId = Guid.NewGuid();
        var entityType = EntityType.Application;

        for (int i = 1; i <= 50; i++)
        {
            var version = new EntityVersion(
                id: Guid.NewGuid(),
                entityId: entityId,
                entityType: entityType,
                version: i,
                entitySnapshot: $"{{\"name\":\"app{i}\"}}",
                timestamp: DateTime.UtcNow.AddMinutes(i),
                action: i == 1 ? AuditAction.ApplicationCreated : AuditAction.ApplicationUpdated,
                userName: "tester",
                userId: Guid.Empty,
                changeDescription: null
            );
            await service.SaveVersionAsync(version);
        }

        var all = await service.GetVersionsAsync(entityId, entityType);
        Assert.Equal(50, all.Count);
    }

    [Fact]
    public async Task SaveVersion_LimitedKeepsOnlyRecentVersions()
    {
        var settings = new AppSettings(
            IncompleteDataWarningEnabled: true,
            LocalLicenseValidationOnly: false,
            HideValidLicenseChip: false,
            VersionHistoryKeepCount: 5
        );
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbVersionHistoryService(store, dir);

        var entityId = Guid.NewGuid();
        var entityType = EntityType.Application;

        for (int i = 1; i <= 20; i++)
        {
            var version = new EntityVersion(
                id: Guid.NewGuid(),
                entityId: entityId,
                entityType: entityType,
                version: i,
                entitySnapshot: $"{{\"name\":\"app{i}\"}}",
                timestamp: DateTime.UtcNow.AddMinutes(i),
                action: i == 1 ? AuditAction.ApplicationCreated : AuditAction.ApplicationUpdated,
                userName: "tester",
                userId: Guid.Empty,
                changeDescription: null
            );
            await service.SaveVersionAsync(version);
        }

        var all = await service.GetVersionsAsync(entityId, entityType);
        Assert.Equal(5, all.Count);

        var versionNumbers = all.Select(v => v.Version).OrderBy(v => v).ToList();
        Assert.Equal(Enumerable.Range(16, 5).ToList(), versionNumbers);
    }

    [Fact]
    public async Task SaveVersion_IndividualEntityLimitsAreIndependent()
    {
        var settings = new AppSettings(
            IncompleteDataWarningEnabled: true,
            LocalLicenseValidationOnly: false,
            HideValidLicenseChip: false,
            VersionHistoryKeepCount: 3
        );
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbVersionHistoryService(store, dir);

        var app1Id = Guid.NewGuid();
        var app2Id = Guid.NewGuid();
        var entityType = EntityType.Application;

        for (int i = 1; i <= 10; i++)
        {
            await service.SaveVersionAsync(CreateVersion(app1Id, entityType, i, $"app1-v{i}"));
        }
        for (int i = 1; i <= 5; i++)
        {
            await service.SaveVersionAsync(CreateVersion(app2Id, entityType, i, $"app2-v{i}"));
        }

        var app1Versions = await service.GetVersionsAsync(app1Id, entityType);
        var app2Versions = await service.GetVersionsAsync(app2Id, entityType);

        Assert.Equal(3, app1Versions.Count);
        Assert.Equal(3, app2Versions.Count);

        var app2Nums = app2Versions.Select(v => v.Version).OrderBy(v => v).ToList();
        Assert.Equal(new List<int> { 3, 4, 5 }, app2Nums);
    }

    [Fact]
    public async Task SaveVersion_DeleteActionAlsoPrunes()
    {
        var settings = new AppSettings(
            IncompleteDataWarningEnabled: true,
            LocalLicenseValidationOnly: false,
            HideValidLicenseChip: false,
            VersionHistoryKeepCount: 5
        );
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbVersionHistoryService(store, dir);

        var entityId = Guid.NewGuid();
        var entityType = EntityType.Application;

        for (int i = 1; i <= 8; i++)
        {
            await service.SaveVersionAsync(CreateVersion(entityId, entityType, i, $"entity-{i}"));
        }
        await service.SaveVersionAsync(new EntityVersion(
            id: Guid.NewGuid(),
            entityId: entityId,
            entityType: entityType,
            version: 9,
            entitySnapshot: null,
            timestamp: DateTime.UtcNow.AddMinutes(9),
            action: AuditAction.ApplicationDeleted,
            userName: "tester",
            userId: Guid.Empty,
            changeDescription: null
        ));

        var all = await service.GetVersionsAsync(entityId, entityType);
        Assert.Equal(5, all.Count);

        var latest = all.First(v => v.Version == 9);
        Assert.Null(latest.EntitySnapshot);
    }

    [Fact]
    public async Task PruneOldVersionsAsync_ManualPruneKeepsSpecifiedCount()
    {
        var settings = new AppSettings(
            IncompleteDataWarningEnabled: true,
            LocalLicenseValidationOnly: false,
            HideValidLicenseChip: false,
            VersionHistoryKeepCount: 0
        );
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbVersionHistoryService(store, dir);

        var entityId = Guid.NewGuid();
        var entityType = EntityType.Application;

        for (int i = 1; i <= 15; i++)
        {
            await service.SaveVersionAsync(CreateVersion(entityId, entityType, i, $"v{i}"));
        }

        await service.PruneOldVersionsAsync(entityId, entityType, 4);

        var all = await service.GetVersionsAsync(entityId, entityType);
        Assert.Equal(4, all.Count);

        var numbers = all.Select(v => v.Version).OrderBy(v => v).ToList();
        Assert.Equal(new List<int> { 12, 13, 14, 15 }, numbers);
    }

    private static EntityVersion CreateVersion(Guid entityId, EntityType entityType, int version, string snapshot)
    {
        return new EntityVersion(
            id: Guid.NewGuid(),
            entityId: entityId,
            entityType: entityType,
            version: version,
            entitySnapshot: snapshot,
            timestamp: DateTime.UtcNow.AddMinutes(version),
            action: version == 1 ? AuditAction.ApplicationCreated : AuditAction.ApplicationUpdated,
            userName: "tester",
            userId: Guid.Empty,
            changeDescription: null
        );
    }

    private sealed class FakeFuseStore : IFuseStore
    {
        private AppSettings _settings;
        private readonly Snapshot _snapshot;

        public FakeFuseStore(AppSettings settings)
        {
            _settings = settings;
            _snapshot = new Snapshot(
                Applications: Array.Empty<Application>(),
                DataStores: Array.Empty<DataStore>(),
                Platforms: Array.Empty<Platform>(),
                ExternalResources: Array.Empty<ExternalResource>(),
                Accounts: Array.Empty<Account>(),
                Identities: Array.Empty<Identity>(),
                Tags: Array.Empty<Tag>(),
                Environments: Array.Empty<EnvironmentInfo>(),
                KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Positions: Array.Empty<Position>(),
                ResponsibilityTypes: Array.Empty<ResponsibilityType>(),
                ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
                Risks: Array.Empty<Risk>(),
                MessageBrokers: Array.Empty<MessageBroker>(),
                Security: new SecurityState(
                    new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow),
                    Array.Empty<SecurityUser>()
                ),
                SecurityContext: new SecurityContext(
                    SecurityPosture.Unrestricted,
                    Array.Empty<FuseRole>(),
                    Array.Empty<FuseUser>(),
                    Array.Empty<FuseApiKey>(),
                    Array.Empty<Session>()
                ),
                AppSettings: settings
            );
        }

        public Task<Snapshot> GetAsync(CancellationToken ct = default)
            => Task.FromResult(_snapshot);

        public Task<T> GetAsync<T>(Func<Snapshot, T> selector, CancellationToken ct = default)
            => Task.FromResult(selector(_snapshot));

        public Task<Snapshot> LoadAsync(CancellationToken ct = default)
            => Task.FromResult(_snapshot);

        public Task SaveAsync(Snapshot snapshot, CancellationToken ct = default)
        {
            _settings = snapshot.AppSettings;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Func<Snapshot, Snapshot> mutate, CancellationToken ct = default)
        {
            _settings = mutate(_snapshot).AppSettings;
            Changed?.Invoke(_snapshot);
            return Task.CompletedTask;
        }

        public Snapshot? Current => _snapshot;
        public event Action<Snapshot>? Changed;
    }
}