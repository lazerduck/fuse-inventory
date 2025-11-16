using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using System.Linq;
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
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: (envs ?? Array.Empty<EnvironmentInfo>()).ToArray(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateEnvironment_EmptyName_Validation()
    {
        var store = NewStore();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("", null, new HashSet<Guid>()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateEnvironment_MissingTag_Validation()
    {
        var store = NewStore(tags: new[] { new Tag(Guid.NewGuid(), "T1", null, null) });
        var missing = Guid.NewGuid();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("Prod", null, new HashSet<Guid> { missing }));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateEnvironment_DuplicateName_Conflict()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Prod", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("prod", null, new HashSet<Guid>()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task CreateEnvironment_Success_AddsEnvironment()
    {
        var tag = new Tag(Guid.NewGuid(), "T", null, null);
        var store = NewStore(tags: new[] { tag });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.CreateEnvironment(new CreateEnvironment("Prod", "desc", new HashSet<Guid> { tag.Id }));

    Assert.True(result.IsSuccess);
    var created = result.Value!;
    Assert.Equal("Prod", created.Name);
    Assert.Single(await service.GetEnvironments(), e => e.Id == created.Id);
    }

    [Fact]
    public async Task UpdateEnvironment_NotFound()
    {
        var store = NewStore();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(Guid.NewGuid(), "X", null, new HashSet<Guid>()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateEnvironment_DuplicateName_Conflict()
    {
        var e1 = new EnvironmentInfo(Guid.NewGuid(), "A", null, new HashSet<Guid>());
        var e2 = new EnvironmentInfo(Guid.NewGuid(), "B", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { e1, e2 });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(e2.Id, "a", null, new HashSet<Guid>()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task UpdateEnvironment_MissingTag_Validation()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(env.Id, "Env", null, new HashSet<Guid> { Guid.NewGuid() }));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task UpdateEnvironment_Success_Updates()
    {
        var tag = new Tag(Guid.NewGuid(), "T1", null, null);
        var env = new EnvironmentInfo(Guid.NewGuid(), "Old", "od", new HashSet<Guid>());
        var store = NewStore(tags: new[] { tag }, envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.UpdateEnvironment(new UpdateEnvironment(env.Id, "New", "nd", new HashSet<Guid> { tag.Id }));

    Assert.True(result.IsSuccess);
    var updated = (await service.GetEnvironments()).First(e => e.Id == env.Id);
    Assert.Equal("New", updated.Name);
    Assert.Equal("nd", updated.Description);
    Assert.Contains(tag.Id, updated.TagIds);
    }

    [Fact]
    public async Task DeleteEnvironment_NotFound()
    {
        var store = NewStore();
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.DeleteEnvironmentAsync(new DeleteEnvironment(Guid.NewGuid()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteEnvironment_Success_Removes()
    {
        var env = new EnvironmentInfo(Guid.NewGuid(), "Env", null, new HashSet<Guid>());
        var store = NewStore(envs: new[] { env });
        var service = new EnvironmentService(store, new TagLookupService(store));

        var result = await service.DeleteEnvironmentAsync(new DeleteEnvironment(env.Id));

    Assert.True(result.IsSuccess);
    Assert.Empty(await service.GetEnvironments());
    }
}
