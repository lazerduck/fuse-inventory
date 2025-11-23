using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using System.Linq;
using Xunit;

namespace Fuse.Tests.Services;

public class TagServiceTests
{
    private static InMemoryFuseStore NewStoreWith(params Tag[] tags)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: tags,
            Environments: Array.Empty<EnvironmentInfo>(),
            SecretProviders: Array.Empty<SecretProvider>(),
                SqlIntegrations: Array.Empty<SqlIntegration>(),
                Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>()),
            KumaIntegrations: Array.Empty<KumaIntegration>()
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateTag_EmptyName_ReturnsValidation()
    {
        var store = NewStoreWith();
        var service = new TagService(store);

        var result = await service.CreateTagAsync(new CreateTag("", null, null));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task CreateTag_DuplicateName_ReturnsConflict()
    {
        var existing = new Tag(Guid.NewGuid(), "Web", null, null);
        var store = NewStoreWith(existing);
        var service = new TagService(store);

        var result = await service.CreateTagAsync(new CreateTag("web", null, null));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task CreateTag_Success_AddsTag()
    {
        var store = NewStoreWith();
        var service = new TagService(store);

        var result = await service.CreateTagAsync(new CreateTag("API", "desc", TagColor.Blue));

    Assert.True(result.IsSuccess);
    var created = result.Value!;
    Assert.Equal("API", created.Name);

    var tags = await service.GetTagsAsync();
    Assert.Single(tags, t => t.Id == created.Id && t.Name == "API");
    }

    [Fact]
    public async Task UpdateTag_NotFound_ReturnsNotFound()
    {
        var store = NewStoreWith();
        var service = new TagService(store);

        var result = await service.UpdateTagAsync(new UpdateTag(Guid.NewGuid(), "n", null, null));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateTag_ConflictOnDuplicateName()
    {
        var t1 = new Tag(Guid.NewGuid(), "A", null, null);
        var t2 = new Tag(Guid.NewGuid(), "B", null, null);
        var store = NewStoreWith(t1, t2);
        var service = new TagService(store);

        var result = await service.UpdateTagAsync(new UpdateTag(t2.Id, "a", null, null));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task UpdateTag_Success_ChangesFields()
    {
        var t = new Tag(Guid.NewGuid(), "Old", "od", TagColor.Gray);
        var store = NewStoreWith(t);
        var service = new TagService(store);

        var result = await service.UpdateTagAsync(new UpdateTag(t.Id, "New", "nd", TagColor.Red));

    Assert.True(result.IsSuccess);
    var updated = (await service.GetTagByIdAsync(t.Id))!;
    Assert.Equal("New", updated.Name);
    Assert.Equal("nd", updated.Description);
    Assert.Equal(TagColor.Red, updated.Color);
    }

    [Fact]
    public async Task DeleteTag_NotFound_ReturnsNotFound()
    {
        var store = NewStoreWith();
        var service = new TagService(store);

        var result = await service.DeleteTagAsync(new DeleteTag(Guid.NewGuid()));

    Assert.False(result.IsSuccess);
    Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteTag_Success_Removes()
    {
        var t = new Tag(Guid.NewGuid(), "Keep", null, null);
        var store = NewStoreWith(t);
        var service = new TagService(store);

        var result = await service.DeleteTagAsync(new DeleteTag(t.Id));

    Assert.True(result.IsSuccess);
    Assert.Null(await service.GetTagByIdAsync(t.Id));
    }
}
