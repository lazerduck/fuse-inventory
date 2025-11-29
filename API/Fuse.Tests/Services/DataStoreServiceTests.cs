using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using System.Linq;
using Xunit;

namespace Fuse.Tests.Services;

public class DataStoreServiceTests
{
    private sealed class TagLookupService : ITagService
    {
        private readonly IFuseStore _store;
        public TagLookupService(IFuseStore store) => _store = store;
        public Task<IReadOnlyList<Tag>> GetTagsAsync() => Task.FromResult((IReadOnlyList<Tag>)_store.Current!.Tags);
        public Task<Tag?> GetTagByIdAsync(Guid id) => Task.FromResult(_store.Current!.Tags.FirstOrDefault(t => t.Id == id));
        public Task<Result<Tag>> CreateTagAsync(CreateTag command) => throw new NotImplementedException();
        public Task<Result<Tag>> UpdateTagAsync(UpdateTag command) => throw new NotImplementedException();
        public Task<Result> DeleteTagAsync(DeleteTag command) => throw new NotImplementedException();
    }

    private static InMemoryFuseStore NewStore(
        IEnumerable<Tag>? tags = null,
        IEnumerable<EnvironmentInfo>? envs = null,
        IEnumerable<DataStore>? ds = null,
        IEnumerable<Platform>? platforms = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: (ds ?? Array.Empty<DataStore>()).ToArray(),
            Platforms: (platforms ?? Array.Empty<Platform>()).ToArray(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Identities: Array.Empty<Identity>(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: (envs ?? Array.Empty<EnvironmentInfo>()).ToArray(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateDataStore_RequiresEnvironment()
    {
        var store = NewStore();
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("D1", "sql", Guid.NewGuid(), null, new Uri("http://x"), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateDataStore_ServerNotFound_ReturnsValidation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("D1", "sql", env.Id, Guid.NewGuid(), new Uri("http://x"), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateDataStore_TagMissing_ReturnsValidation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("D1", "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid> { Guid.NewGuid() }));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }



    [Fact]
    public async Task CreateDataStore_DuplicatePerEnvironment()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var ds = new DataStore(Guid.NewGuid(), "D1", null, "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, ds: new[] { ds });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("d1", "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task CreateDataStore_Success()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("D1", "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid>()));
    Assert.True(result.IsSuccess);
    Assert.Single(await service.GetDataStoresAsync(), d => d.Name == "D1");
    }

    [Fact]
    public async Task CreateDataStore_AllowsMissingConnectionUri()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("D1", "sql", env.Id, null, null, new HashSet<Guid>()));
    Assert.True(result.IsSuccess);
    Assert.Null(result.Value!.ConnectionUri);
    }

    [Fact]
    public async Task UpdateDataStore_NotFound()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.UpdateDataStoreAsync(new UpdateDataStore(Guid.NewGuid(), "D1", "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateDataStore_DuplicatePerEnvironment()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var d1 = new DataStore(Guid.NewGuid(), "A", null, "sql", env.Id, null, new Uri("http://a"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var d2 = new DataStore(Guid.NewGuid(), "B", null, "sql", env.Id, null, new Uri("http://b"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, ds: new[] { d1, d2 });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.UpdateDataStoreAsync(new UpdateDataStore(d2.Id, "a", "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid>()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }



    [Fact]
    public async Task UpdateDataStore_Success()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var ds = new DataStore(Guid.NewGuid(), "Old", null, "sql", env.Id, null, new Uri("http://old"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, ds: new[] { ds });
        var service = new DataStoreService(store, new TagLookupService(store));
        var res = await service.UpdateDataStoreAsync(new UpdateDataStore(ds.Id, "New", "nosql", env.Id, null, new Uri("http://new"), new HashSet<Guid>()));
    Assert.True(res.IsSuccess);
    var got = await service.GetDataStoreByIdAsync(ds.Id);
    Assert.Equal("New", got!.Name);
    Assert.Equal("nosql", got.Kind);
    Assert.Equal(new Uri("http://new"), got.ConnectionUri);
    }

    [Fact]
    public async Task UpdateDataStore_AllowsClearingConnectionUri()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var ds = new DataStore(Guid.NewGuid(), "HasUri", null, "sql", env.Id, null, new Uri("http://old"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, ds: new[] { ds });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.UpdateDataStoreAsync(new UpdateDataStore(ds.Id, "HasUri", "sql", env.Id, null, null, new HashSet<Guid>()));
    Assert.True(result.IsSuccess);
    Assert.Null(result.Value!.ConnectionUri);
    }

    [Fact]
    public async Task DeleteDataStore_Success_Removes()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var ds = new DataStore(Guid.NewGuid(), "D", null, "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, ds: new[] { ds });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.DeleteDataStoreAsync(new DeleteDataStore(ds.Id));
    Assert.True(result.IsSuccess);
    Assert.Empty(await service.GetDataStoresAsync());
    }
}
