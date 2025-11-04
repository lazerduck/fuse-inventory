using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using FluentAssertions;
using Xunit;

namespace Fuse.Tests.Services;

public class ServerServiceTests
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
        IEnumerable<Server>? servers = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Servers: (servers ?? Array.Empty<Server>()).ToArray(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: (envs ?? Array.Empty<EnvironmentInfo>()).ToArray()
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateServer_RequiresEnvironment()
    {
        var store = NewStore();
        var service = new ServerService(store, new TagLookupService(store));
        var result = await service.CreateServerAsync(new CreateServer("S1", "host", ServerOperatingSystem.Linux, Guid.NewGuid(), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateServer_MissingTag_ReturnsValidation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new ServerService(store, new TagLookupService(store));

        var result = await service.CreateServerAsync(new CreateServer("S1", "host", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid> { Guid.NewGuid() }));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateServer_EmptyNameOrHostname_ReturnsValidation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new ServerService(store, new TagLookupService(store));

        (await service.CreateServerAsync(new CreateServer("", "host", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>()))).IsSuccess.Should().BeFalse();
        (await service.CreateServerAsync(new CreateServer("S1", "", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>()))).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task CreateServer_DuplicatePerEnvironment()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var existing = new Server(Guid.NewGuid(), "S1", null, "h", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, servers: new[] { existing });
        var service = new ServerService(store, new TagLookupService(store));
        var result = await service.CreateServerAsync(new CreateServer("s1", "host", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateServer_Success()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new ServerService(store, new TagLookupService(store));
        var result = await service.CreateServerAsync(new CreateServer("S1", "host", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>()));
        result.IsSuccess.Should().BeTrue();
        (await service.GetServersAsync()).Should().ContainSingle(s => s.Name == "S1");
    }

    [Fact]
    public async Task UpdateServer_NotFound()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new ServerService(store, new TagLookupService(store));
        var result = await service.UpdateServerAsync(new UpdateServer(Guid.NewGuid(), "S1", "h", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateServer_DuplicatePerEnvironment()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var s1 = new Server(Guid.NewGuid(), "A", null, "h1", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var s2 = new Server(Guid.NewGuid(), "B", null, "h2", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, servers: new[] { s1, s2 });
        var service = new ServerService(store, new TagLookupService(store));
        var result = await service.UpdateServerAsync(new UpdateServer(s2.Id, "a", "h2", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task UpdateServer_Success()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var s = new Server(Guid.NewGuid(), "Old", null, "old", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, servers: new[] { s });
        var service = new ServerService(store, new TagLookupService(store));
        var res = await service.UpdateServerAsync(new UpdateServer(s.Id, "New", "new", ServerOperatingSystem.Windows, env.Id, new HashSet<Guid>()));
        res.IsSuccess.Should().BeTrue();
        var got = await service.GetServerByIdAsync(s.Id);
        got!.Name.Should().Be("New");
        got.Hostname.Should().Be("new");
        got.OperatingSystem.Should().Be(ServerOperatingSystem.Windows);
    }

    [Fact]
    public async Task DeleteServer_NotFound()
    {
        var store = NewStore();
        var service = new ServerService(store, new TagLookupService(store));
        var result = await service.DeleteServerAsync(new DeleteServer(Guid.NewGuid()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteServer_Success_Removes()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var s = new Server(Guid.NewGuid(), "A", null, "h", ServerOperatingSystem.Linux, env.Id, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(envs: new[] { env }, servers: new[] { s });
        var service = new ServerService(store, new TagLookupService(store));
        var result = await service.DeleteServerAsync(new DeleteServer(s.Id));
        result.IsSuccess.Should().BeTrue();
        (await service.GetServersAsync()).Should().BeEmpty();
    }
}
