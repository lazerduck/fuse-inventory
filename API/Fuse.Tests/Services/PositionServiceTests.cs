using Fuse.Core.Areas.Position;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Services;

public class PositionServiceTests
{
    private static Position Position(string name = "Owner") =>
        new(Guid.NewGuid(), name, "description", [], DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1));

    private static PositionService CreateService(InMemoryFuseStore store, FakeAuditService? audit = null) =>
        new(store, new FakeTagService(store), audit ?? new FakeAuditService(), new FakeCurrentUser());

    [Fact]
    public async Task Queries_ReturnStoredPositionsAndFindById()
    {
        var expected = Position();
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [expected] });
        var service = CreateService(store);

        Assert.Equal([expected], await service.GetPositionsAsync());
        Assert.Equal(expected, await service.GetPositionByIdAsync(expected.Id));
        Assert.Null(await service.GetPositionByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Create_RejectsBlankName()
    {
        var result = await CreateService(new InMemoryFuseStore()).CreatePositionAsync(new(" ", null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Create_RejectsUnknownTag()
    {
        var result = await CreateService(new InMemoryFuseStore())
            .CreatePositionAsync(new("Owner", null, [Guid.NewGuid()]));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Create_RejectsCaseInsensitiveDuplicate()
    {
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [Position("Owner")] });

        var result = await CreateService(store).CreatePositionAsync(new("owner", null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task Create_PersistsPositionAndWritesAuditLog()
    {
        var tag = new Tag(Guid.NewGuid(), "team", null, null);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Tags = [tag] });
        var audit = new FakeAuditService();

        var result = await CreateService(store, audit).CreatePositionAsync(new("Owner", "desc", [tag.Id]));

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value, Assert.Single(store.Current!.Positions));
        Assert.Contains(tag.Id, result.Value!.TagIds);
        Assert.Equal(AuditAction.PositionCreated, Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task Update_RejectsBlankNameBeforeLookup()
    {
        var result = await CreateService(new InMemoryFuseStore())
            .UpdatePositionAsync(new(Guid.NewGuid(), "", null, null));

        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Update_RejectsMissingPositionUnknownTagAndDuplicateName()
    {
        var first = Position("First");
        var second = Position("Second");
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [first, second] });
        var service = CreateService(store);

        Assert.Equal(ErrorType.NotFound, (await service.UpdatePositionAsync(new(Guid.NewGuid(), "x", null, null))).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.UpdatePositionAsync(new(first.Id, "x", null, [Guid.NewGuid()]))).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await service.UpdatePositionAsync(new(first.Id, "SECOND", null, null))).ErrorType);
    }

    [Fact]
    public async Task Update_PersistsFieldsAndWritesAuditLog()
    {
        var existing = Position();
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [existing] });
        var audit = new FakeAuditService();

        var result = await CreateService(store, audit).UpdatePositionAsync(new(existing.Id, "New", "changed", null));

        Assert.True(result.IsSuccess);
        Assert.Equal("New", store.Current!.Positions.Single().Name);
        Assert.True(result.Value!.UpdatedAt > existing.UpdatedAt);
        Assert.Equal(AuditAction.PositionUpdated, Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task Delete_RejectsMissingAndReferencedPosition()
    {
        var existing = Position();
        var assignment = new ResponsibilityAssignment(Guid.NewGuid(), existing.Id, Guid.NewGuid(), Guid.NewGuid(),
            ResponsibilityScope.All, null, null, false, DateTime.UtcNow, DateTime.UtcNow);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [existing], ResponsibilityAssignments = [assignment] });
        var service = CreateService(store);

        Assert.Equal(ErrorType.NotFound, (await service.DeletePositionAsync(new(Guid.NewGuid()))).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await service.DeletePositionAsync(new(existing.Id))).ErrorType);
    }

    [Fact]
    public async Task Delete_RemovesPositionAndWritesAuditLog()
    {
        var existing = Position();
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [existing] });
        var audit = new FakeAuditService();

        var result = await CreateService(store, audit).DeletePositionAsync(new(existing.Id));

        Assert.True(result.IsSuccess);
        Assert.Empty(store.Current!.Positions);
        Assert.Equal(AuditAction.PositionDeleted, Assert.Single(audit.Logs).Action);
    }
}
