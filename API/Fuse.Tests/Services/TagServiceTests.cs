using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using FluentAssertions;
using Xunit;

namespace Fuse.Tests.Services;

public class TagServiceTests
{
    private static InMemoryFuseStore NewStoreWith(params Tag[] tags)
    {
        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Servers: Array.Empty<Server>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Tags: tags,
            Environments: Array.Empty<EnvironmentInfo>()
        );
        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateTag_EmptyName_ReturnsValidation()
    {
        var store = NewStoreWith();
        var service = new TagService(store);

        var result = await service.CreateTagAsync(new CreateTag("", null, null));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateTag_DuplicateName_ReturnsConflict()
    {
        var existing = new Tag(Guid.NewGuid(), "Web", null, null);
        var store = NewStoreWith(existing);
        var service = new TagService(store);

        var result = await service.CreateTagAsync(new CreateTag("web", null, null));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateTag_Success_AddsTag()
    {
        var store = NewStoreWith();
        var service = new TagService(store);

        var result = await service.CreateTagAsync(new CreateTag("API", "desc", TagColor.Blue));

        result.IsSuccess.Should().BeTrue();
        var created = result.Value!;
        created.Name.Should().Be("API");

        var tags = await service.GetTagsAsync();
        tags.Should().ContainSingle(t => t.Id == created.Id && t.Name == "API");
    }

    [Fact]
    public async Task UpdateTag_NotFound_ReturnsNotFound()
    {
        var store = NewStoreWith();
        var service = new TagService(store);

        var result = await service.UpdateTagAsync(new UpdateTag(Guid.NewGuid(), "n", null, null));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateTag_ConflictOnDuplicateName()
    {
        var t1 = new Tag(Guid.NewGuid(), "A", null, null);
        var t2 = new Tag(Guid.NewGuid(), "B", null, null);
        var store = NewStoreWith(t1, t2);
        var service = new TagService(store);

        var result = await service.UpdateTagAsync(new UpdateTag(t2.Id, "a", null, null));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task UpdateTag_Success_ChangesFields()
    {
        var t = new Tag(Guid.NewGuid(), "Old", "od", TagColor.Gray);
        var store = NewStoreWith(t);
        var service = new TagService(store);

        var result = await service.UpdateTagAsync(new UpdateTag(t.Id, "New", "nd", TagColor.Red));

        result.IsSuccess.Should().BeTrue();
        var updated = (await service.GetTagByIdAsync(t.Id))!;
        updated.Name.Should().Be("New");
        updated.Description.Should().Be("nd");
        updated.Color.Should().Be(TagColor.Red);
    }

    [Fact]
    public async Task DeleteTag_NotFound_ReturnsNotFound()
    {
        var store = NewStoreWith();
        var service = new TagService(store);

        var result = await service.DeleteTagAsync(new DeleteTag(Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteTag_Success_Removes()
    {
        var t = new Tag(Guid.NewGuid(), "Keep", null, null);
        var store = NewStoreWith(t);
        var service = new TagService(store);

        var result = await service.DeleteTagAsync(new DeleteTag(t.Id));

        result.IsSuccess.Should().BeTrue();
        (await service.GetTagByIdAsync(t.Id)).Should().BeNull();
    }
}
