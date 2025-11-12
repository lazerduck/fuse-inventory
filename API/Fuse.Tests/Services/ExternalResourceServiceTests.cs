using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using FluentAssertions;
using Xunit;

namespace Fuse.Tests.Services;

public class ExternalResourceServiceTests
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
        IEnumerable<ExternalResource>? res = null)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: (res ?? Array.Empty<ExternalResource>()).ToArray(),
            Accounts: Array.Empty<Account>(),
            Tags: (tags ?? Array.Empty<Tag>()).ToArray(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateExternalResource_DuplicateName()
    {
        var existing = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { existing });
        var service = new ExternalResourceService(store, new TagLookupService(store));
        var result = await service.CreateExternalResourceAsync(new CreateExternalResource("res", null, new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateExternalResource_Success()
    {
        var store = NewStore();
        var service = new ExternalResourceService(store, new TagLookupService(store));
        var result = await service.CreateExternalResourceAsync(new CreateExternalResource("Res", "d", new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeTrue();
        (await service.GetExternalResourcesAsync()).Should().ContainSingle(r => r.Name == "Res");
    }

    [Fact]
    public async Task CreateExternalResource_AllowsMissingUri()
    {
        var store = NewStore();
        var service = new ExternalResourceService(store, new TagLookupService(store));
        var result = await service.CreateExternalResourceAsync(new CreateExternalResource("Res", null, null, new HashSet<Guid>()));
        result.IsSuccess.Should().BeTrue();
        result.Value!.ResourceUri.Should().BeNull();
    }

    [Fact]
    public async Task CreateExternalResource_EmptyName_ReturnsValidation()
    {
        var store = NewStore();
        var service = new ExternalResourceService(store, new TagLookupService(store));
        var result = await service.CreateExternalResourceAsync(new CreateExternalResource("", null, new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateExternalResource_TagMissing_ReturnsValidation()
    {
        var store = NewStore();
        var service = new ExternalResourceService(store, new TagLookupService(store));
        var result = await service.CreateExternalResourceAsync(new CreateExternalResource("Res", null, new Uri("http://x"), new HashSet<Guid> { Guid.NewGuid() }));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task UpdateExternalResource_NotFound()
    {
        var store = NewStore();
        var service = new ExternalResourceService(store, new TagLookupService(store));
        var result = await service.UpdateExternalResourceAsync(new UpdateExternalResource(Guid.NewGuid(), "R", null, new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateExternalResource_DuplicateName()
    {
        var r1 = new ExternalResource(Guid.NewGuid(), "A", null, new Uri("http://a"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var r2 = new ExternalResource(Guid.NewGuid(), "B", null, new Uri("http://b"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { r1, r2 });
        var service = new ExternalResourceService(store, new TagLookupService(store));
        var result = await service.UpdateExternalResourceAsync(new UpdateExternalResource(r2.Id, "a", null, new Uri("http://x"), new HashSet<Guid>()));
        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task UpdateExternalResource_Success()
    {
        var r = new ExternalResource(Guid.NewGuid(), "Old", null, new Uri("http://old"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { r });
        var service = new ExternalResourceService(store, new TagLookupService(store));
        var res = await service.UpdateExternalResourceAsync(new UpdateExternalResource(r.Id, "New", "d", new Uri("http://new"), new HashSet<Guid>()));
        res.IsSuccess.Should().BeTrue();
        var got = await service.GetExternalResourceByIdAsync(r.Id);
        got!.Name.Should().Be("New");
        got.Description.Should().Be("d");
        got.ResourceUri.Should().Be(new Uri("http://new"));
    }

    [Fact]
    public async Task UpdateExternalResource_AllowsClearingUri()
    {
        var existing = new ExternalResource(Guid.NewGuid(), "Res", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { existing });
        var service = new ExternalResourceService(store, new TagLookupService(store));
        var result = await service.UpdateExternalResourceAsync(new UpdateExternalResource(existing.Id, "Res", null, null, new HashSet<Guid>()));
        result.IsSuccess.Should().BeTrue();
        result.Value!.ResourceUri.Should().BeNull();
    }

    [Fact]
    public async Task DeleteExternalResource_Success()
    {
        var r = new ExternalResource(Guid.NewGuid(), "R", null, new Uri("http://x"), new HashSet<Guid>(), DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(res: new[] { r });
        var service = new ExternalResourceService(store, new TagLookupService(store));
        var result = await service.DeleteExternalResourceAsync(new DeleteExternalResource(r.Id));
        result.IsSuccess.Should().BeTrue();
        (await service.GetExternalResourcesAsync()).Should().BeEmpty();
    }
}
