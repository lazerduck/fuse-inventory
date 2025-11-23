using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using System.Linq;
using Xunit;

namespace Fuse.Tests.Services;

public class PlatformServiceTests
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
        IEnumerable<Platform>? platforms = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: (platforms ?? Array.Empty<Platform>()).ToArray(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
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
    public async Task CreatePlatform_MissingTag_ReturnsValidation()
    {
        var store = NewStore();
        var service = new PlatformService(store, new TagLookupService(store));

        var result = await service.CreatePlatformAsync(new CreatePlatform("P1", TagIds: new HashSet<Guid> { Guid.NewGuid() }));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreatePlatform_EmptyName_ReturnsValidation()
    {
        var store = NewStore();
        var service = new PlatformService(store, new TagLookupService(store));

    Assert.False((await service.CreatePlatformAsync(new CreatePlatform(""))).IsSuccess);
    }

    [Fact]
    public async Task CreatePlatform_DuplicateName()
    {
        var existing = new Platform(Guid.NewGuid(), "P1", null, null, null, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(platforms: new[] { existing });
        var service = new PlatformService(store, new TagLookupService(store));
        var result = await service.CreatePlatformAsync(new CreatePlatform("p1"));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task CreatePlatform_Success()
    {
        var store = NewStore();
        var service = new PlatformService(store, new TagLookupService(store));
        var result = await service.CreatePlatformAsync(new CreatePlatform("P1", DnsName: "host.example.com"));
    Assert.True(result.IsSuccess);
    Assert.Single(await service.GetPlatformsAsync(), s => s.DisplayName == "P1");
    }

    [Fact]
    public async Task UpdatePlatform_NotFound()
    {
        var store = NewStore();
        var service = new PlatformService(store, new TagLookupService(store));
        var result = await service.UpdatePlatformAsync(new UpdatePlatform(Guid.NewGuid(), "P1"));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdatePlatform_Duplicate()
    {
        var p1 = new Platform(Guid.NewGuid(), "A", null, null, null, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var p2 = new Platform(Guid.NewGuid(), "B", null, null, null, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(platforms: new[] { p1, p2 });
        var service = new PlatformService(store, new TagLookupService(store));
        var result = await service.UpdatePlatformAsync(new UpdatePlatform(p2.Id, "a"));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task UpdatePlatform_Success()
    {
        var p = new Platform(Guid.NewGuid(), "Old", "old.example.com", "linux", PlatformKind.Server, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(platforms: new[] { p });
        var service = new PlatformService(store, new TagLookupService(store));
        var res = await service.UpdatePlatformAsync(new UpdatePlatform(p.Id, "New", DnsName: "new.example.com", Os: "windows", Kind: PlatformKind.Cluster));
    Assert.True(res.IsSuccess);
    var got = await service.GetPlatformByIdAsync(p.Id);
    Assert.Equal("New", got!.DisplayName);
    Assert.Equal("new.example.com", got.DnsName);
    Assert.Equal("windows", got.Os);
    Assert.Equal(PlatformKind.Cluster, got.Kind);
    }

    [Fact]
    public async Task DeletePlatform_NotFound()
    {
        var store = NewStore();
        var service = new PlatformService(store, new TagLookupService(store));
        var result = await service.DeletePlatformAsync(new DeletePlatform(Guid.NewGuid()));
    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeletePlatform_Success_Removes()
    {
        var p = new Platform(Guid.NewGuid(), "A", null, null, null, null, null, new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(platforms: new[] { p });
        var service = new PlatformService(store, new TagLookupService(store));
        var result = await service.DeletePlatformAsync(new DeletePlatform(p.Id));
    Assert.True(result.IsSuccess);
    Assert.Empty(await service.GetPlatformsAsync());
    }
}
