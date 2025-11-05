using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using FluentAssertions;
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
        IEnumerable<Server>? servers = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: (ds ?? Array.Empty<DataStore>()).ToArray(),
            Servers: (servers ?? Array.Empty<Server>()).ToArray(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: (envs ?? Array.Empty<EnvironmentInfo>()).ToArray()
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateDataStore_RequiresEnvironment()
    {
        var store = NewStore();
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("D1", "sql", Guid.NewGuid(), null, new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateDataStore_ServerNotFound_ReturnsValidation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("D1", "sql", env.Id, Guid.NewGuid(), new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateDataStore_TagMissing_ReturnsValidation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("D1", "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid> { Guid.NewGuid() }));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateDataStore_ServerMustMatchEnvironment()
    {
        var env1 = new EnvironmentInfo(Guid.NewGuid(), "E1", null, new HashSet<Guid>());
        var env2 = new EnvironmentInfo(Guid.NewGuid(), "E2", null, new HashSet<Guid>());
        var server = new Server(Guid.NewGuid(), "S", null, "h", ServerOperatingSystem.Linux, env2.Id, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env1, env2 }, servers: new[] { server });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("D1", "sql", env1.Id, server.Id, new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateDataStore_DuplicatePerEnvironment()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var ds = new DataStore(Guid.NewGuid(), "D1", null, "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, ds: new[] { ds });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("d1", "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateDataStore_Success()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.CreateDataStoreAsync(new CreateDataStore("D1", "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeTrue();
        (await service.GetDataStoresAsync()).Should().ContainSingle(d => d.Name == "D1");
    }

    [Fact]
    public async Task UpdateDataStore_NotFound()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.UpdateDataStoreAsync(new UpdateDataStore(Guid.NewGuid(), "D1", "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
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
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task UpdateDataStore_ServerMustMatchEnvironment()
    {
        var e1 = new EnvironmentInfo(Guid.NewGuid(), "E1", null, new HashSet<Guid>());
        var e2 = new EnvironmentInfo(Guid.NewGuid(), "E2", null, new HashSet<Guid>());
        var server = new Server(Guid.NewGuid(), "S", null, "h", ServerOperatingSystem.Linux, e2.Id, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var ds = new DataStore(Guid.NewGuid(), "D", null, "sql", e1.Id, null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { e1, e2 }, ds: new[] { ds }, servers: new[] { server });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.UpdateDataStoreAsync(new UpdateDataStore(ds.Id, "D", "sql", e1.Id, server.Id, new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task UpdateDataStore_Success()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var ds = new DataStore(Guid.NewGuid(), "Old", null, "sql", env.Id, null, new Uri("http://old"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, ds: new[] { ds });
        var service = new DataStoreService(store, new TagLookupService(store));
        var res = await service.UpdateDataStoreAsync(new UpdateDataStore(ds.Id, "New", "nosql", env.Id, null, new Uri("http://new"), new HashSet<Guid>()));
        res.IsSuccess.Should().BeTrue();
        var got = await service.GetDataStoreByIdAsync(ds.Id);
        got!.Name.Should().Be("New");
        got.Kind.Should().Be("nosql");
        got.ConnectionUri.Should().Be(new Uri("http://new"));
    }

    [Fact]
    public async Task DeleteDataStore_Success_Removes()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "E", null, new HashSet<Guid>());
        var ds = new DataStore(Guid.NewGuid(), "D", null, "sql", env.Id, null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, ds: new[] { ds });
        var service = new DataStoreService(store, new TagLookupService(store));
        var result = await service.DeleteDataStoreAsync(new DeleteDataStore(ds.Id));
        result.IsSuccess.Should().BeTrue();
        (await service.GetDataStoresAsync()).Should().BeEmpty();
    }
}
