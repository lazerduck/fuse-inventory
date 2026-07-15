using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.Helpers;
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
            Identities: Array.Empty<Identity>(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: (envs ?? Array.Empty<EnvironmentInfo>()).ToArray(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
                SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(), Positions: Array.Empty<Position>(), ResponsibilityTypes: Array.Empty<ResponsibilityType>(), ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>()),
                SecurityContextHelper.Get
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
    public async Task CreateCluster_WithNodesAndMultipleAddresses_Succeeds()
    {
        var store = NewStore();
        var service = new PlatformService(store, new TagLookupService(store));
        var result = await service.CreatePlatformAsync(new CreatePlatform(
            "Cluster", Kind: PlatformKind.Cluster, IpAddresses: ["10.0.0.1", "10.0.0.2"],
            Nodes: [new(null, "node-1", IpAddresses: ["10.0.1.1"])]));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.IpAddresses.Count);
        var node = Assert.Single(result.Value.Nodes!);
        Assert.NotEqual(Guid.Empty, node.Id);
        Assert.Equal("10.0.1.1", Assert.Single(node.IpAddresses));
    }

    [Fact]
    public async Task CreateNonCluster_WithNodes_ReturnsValidation()
    {
        var store = NewStore();
        var service = new PlatformService(store, new TagLookupService(store));

        var result = await service.CreatePlatformAsync(new CreatePlatform(
            "Server", Kind: PlatformKind.Server, Nodes: [new(null, "node-1")]));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Theory]
    [InlineData("999.1.1.1")]
    [InlineData("127.1")]
    [InlineData("192.168.001.1")]
    [InlineData("not-an-ip")]
    public async Task CreatePlatform_InvalidIpAddress_ReturnsValidation(string address)
    {
        var store = NewStore();
        var service = new PlatformService(store, new TagLookupService(store));

        var result = await service.CreatePlatformAsync(new CreatePlatform("Server", IpAddresses: [address]));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateCluster_AcceptsIpv6Addresses()
    {
        var store = NewStore();
        var service = new PlatformService(store, new TagLookupService(store));

        var result = await service.CreatePlatformAsync(new CreatePlatform(
            "Cluster", Kind: PlatformKind.Cluster, IpAddresses: ["2001:db8::1"],
            Nodes: [new(null, "node-1", IpAddresses: ["2001:db8::2"])]));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateCluster_PreservesExistingNodeId()
    {
        var nodeId = Guid.NewGuid();
        var platform = new Platform(Guid.NewGuid(), "Cluster", null, null, PlatformKind.Cluster, [], null, [],
            DateTime.UtcNow, DateTime.UtcNow, [new(nodeId, "node-1", null, null, [], null)]);
        var store = NewStore(platforms: [platform]);
        var service = new PlatformService(store, new TagLookupService(store));

        var result = await service.UpdatePlatformAsync(new UpdatePlatform(platform.Id, platform.DisplayName,
            Kind: PlatformKind.Cluster, Nodes: [new(nodeId, "renamed")]));

        Assert.True(result.IsSuccess);
        Assert.Equal(nodeId, Assert.Single(result.Value!.Nodes!).Id);
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
