using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using FluentAssertions;
using Xunit;

namespace Fuse.Tests.Services;

public class EnvironmentServiceTests
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
        IEnumerable<EnvironmentInfo>? envs = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Servers: Array.Empty<Server>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: (envs ?? Array.Empty<EnvironmentInfo>()).ToArray()
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateEnvironment_EmptyName_Validation()
    {
        var store = NewStore();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("", null, new HashSet<Guid>()));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateEnvironment_MissingTag_Validation()
    {
        var store = NewStore(tags: new[] { new Tag(Guid.NewGuid(), "T1", null, null) });
        var missing = Guid.NewGuid();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("Prod", null, new HashSet<Guid> { missing }));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateEnvironment_DuplicateName_Conflict()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Prod", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("prod", null, new HashSet<Guid>()));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateEnvironment_Success_AddsEnvironment()
    {
        var tag = new Tag(Guid.NewGuid(), "T", null, null);
        var store = NewStore(tags: new[] { tag });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("Prod", "desc", new HashSet<Guid> { tag.Id }));

        result.IsSuccess.Should().BeTrue();
        var created = result.Value!;
        created.Name.Should().Be("Prod");
        (await service.GetEnvironments()).Should().ContainSingle(e => e.Id == created.Id);
    }

    [Fact]
    public async Task UpdateEnvironment_NotFound()
    {
        var store = NewStore();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(Guid.NewGuid(), "X", null, new HashSet<Guid>()));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateEnvironment_DuplicateName_Conflict()
    {
        var e1 = new EnvironmentInfo(Guid.NewGuid(), "A", null, new HashSet<Guid>());
        var e2 = new EnvironmentInfo(Guid.NewGuid(), "B", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { e1, e2 });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(e2.Id, "a", null, new HashSet<Guid>()));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task UpdateEnvironment_MissingTag_Validation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(env.Id, "Env", null, new HashSet<Guid> { Guid.NewGuid() }));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task UpdateEnvironment_Success_Updates()
    {
        var tag = new Tag(Guid.NewGuid(), "T1", null, null);
        var env = new EnvironmentInfo(Guid.NewGuid(), "Old", "od", new HashSet<Guid>());
        var store = NewStore(tags: new[] { tag }, envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(env.Id, "New", "nd", new HashSet<Guid> { tag.Id }));

        result.IsSuccess.Should().BeTrue();
        var updated = (await service.GetEnvironments()).First(e => e.Id == env.Id);
        updated.Name.Should().Be("New");
        updated.Description.Should().Be("nd");
        updated.TagIds.Should().Contain(tag.Id);
    }

    [Fact]
    public async Task DeleteEnvironment_NotFound()
    {
        var store = NewStore();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.DeleteEnvironmentAsync(new DeleteEnvironment(Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteEnvironment_Success_Removes()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.DeleteEnvironmentAsync(new DeleteEnvironment(env.Id));

        result.IsSuccess.Should().BeTrue();
        (await service.GetEnvironments()).Should().BeEmpty();
    }
}
