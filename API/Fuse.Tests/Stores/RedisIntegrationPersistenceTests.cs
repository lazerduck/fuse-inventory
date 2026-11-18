using System.IO.Compression;
using Fuse.Core.Areas.Config;
using Fuse.Core.Areas.Logging;
using Fuse.Core.Commands;
using Fuse.Core.Configs;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Data.Stores;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Stores;

public class RedisIntegrationPersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"fuse-redis-store-{Guid.NewGuid()}");

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private static Snapshot Fixture()
    {
        var now = DateTime.UtcNow;
        var environment = new EnvironmentInfo(Guid.NewGuid(), "Lab", null, []);
        var dataStore = new DataStore(Guid.NewGuid(), "Redis", null, "Redis", environment.Id,
            null, new Uri("redis://redis:6379"), [], now, now);
        var integration = new RedisIntegration(Guid.NewGuid(), "Inspector", dataStore.Id,
            dataStore.ConnectionUri!, "inspector", "private-connection-secret", now, now);
        return new InMemoryFuseStore().Current! with
        {
            Environments = [environment], DataStores = [dataStore], RedisIntegrations = [integration]
        };
    }

    [Fact]
    public async Task ConnectionsSurviveReloadAndBackup_AndDeletionIsPersisted()
    {
        var fixture = Fixture();
        using var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _directory });
        await store.SaveAsync(fixture);
        using var reopened = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _directory });
        Assert.Equal(fixture.RedisIntegrations, (await reopened.GetAsync()).RedisIntegrations);

        await reopened.CreateBackupAsync();
        using (var backup = ZipFile.OpenRead(Path.Combine(_directory, "fuse-data.bak")))
            Assert.NotNull(backup.GetEntry("redisintegrations.json"));

        await reopened.UpdateAsync(s => s with { RedisIntegrations = [] });
        Assert.Empty((await reopened.LoadAsync()).RedisIntegrations);
    }

    [Fact]
    public async Task OlderStoreWithoutRedisFile_LoadsWithNoConnections()
    {
        using var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _directory });
        await store.SaveAsync(Fixture() with { RedisIntegrations = [] });
        File.Delete(Path.Combine(_directory, "redisintegrations.json"));
        Assert.Empty((await store.LoadAsync()).RedisIntegrations);
    }

    [Fact]
    public async Task InventoryImportPreservesConnections_ExportDoesNotIncludeTheirCredentials()
    {
        var store = new InMemoryFuseStore(Fixture());
        var before = store.Current!.RedisIntegrations;
        var service = new ConfigService(store, new FakeAuditService(), Mock.Of<ILogService>(), new FakeCurrentUser());
        var exported = await service.ExportAsync(ConfigFormat.Json);
        Assert.DoesNotContain("private-connection-secret", exported);
        Assert.DoesNotContain("redisintegrations", exported, StringComparison.OrdinalIgnoreCase);
        await service.ImportAsync(exported, ConfigFormat.Json);
        Assert.Same(before, store.Current!.RedisIntegrations);
    }

    [Fact]
    public void InvalidRedisReferencesAndDuplicateIds_AreRejected()
    {
        var snapshot = Fixture();
        Assert.Empty(SnapshotValidator.Validate(snapshot));
        Assert.Contains(SnapshotValidator.Validate(snapshot with { DataStores = [] }),
            error => error.Contains("Redis integration") && error.Contains("not found"));
        Assert.Contains(SnapshotValidator.Validate(snapshot with
        {
            DataStores = [snapshot.DataStores[0] with { Kind = "sql" }]
        }), error => error.Contains("must have kind Redis"));
        Assert.Contains("Duplicate Redis integration Ids detected", SnapshotValidator.Validate(snapshot with
        {
            RedisIntegrations = [snapshot.RedisIntegrations[0], snapshot.RedisIntegrations[0]]
        }));
    }

    [Fact]
    public async Task AttachedConnectionPreventsDataStoreDeletionOrChangingKind()
    {
        var store = new InMemoryFuseStore(Fixture());
        var dataStore = store.Current!.DataStores[0];
        var service = new DataStoreService(store, Mock.Of<ITagService>());
        var deletion = await service.DeleteDataStoreAsync(new DeleteDataStore(dataStore.Id));
        Assert.False(deletion.IsSuccess);
        Assert.Equal(ErrorType.Conflict, deletion.ErrorType);
        var change = await service.UpdateDataStoreAsync(new UpdateDataStore(dataStore.Id, dataStore.Name,
            "sql", dataStore.EnvironmentId, null, null));
        Assert.False(change.IsSuccess);
        Assert.Equal(ErrorType.Conflict, change.ErrorType);
        Assert.Equal("Redis", store.Current!.DataStores[0].Kind);
        await store.UpdateAsync(s => s with { RedisIntegrations = [] });
        Assert.True((await service.DeleteDataStoreAsync(new DeleteDataStore(dataStore.Id))).IsSuccess);
    }
}
