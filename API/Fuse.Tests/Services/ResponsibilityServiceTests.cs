using Fuse.Core.Areas.Responsibility;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Services;

public class ResponsibilityServiceTests
{
    private static ResponsibilityType Type(string name = "Owner") =>
        new(Guid.NewGuid(), name, "description", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1));

    private static Position Position() =>
        new(Guid.NewGuid(), "Position", null, [], DateTime.UtcNow, DateTime.UtcNow);

    private static Application Application() =>
        new(Guid.NewGuid(), "App", null, null, null, null, null, null, null, [], [], [], DateTime.UtcNow, DateTime.UtcNow);

    private static ResponsibilityAssignment Assignment(Guid positionId, Guid typeId, Guid applicationId) =>
        new(Guid.NewGuid(), positionId, typeId, applicationId, ResponsibilityScope.All, null, "notes", true,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1));

    [Fact]
    public async Task TypeQueries_ReturnStoredTypesAndFindById()
    {
        var expected = Type();
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { ResponsibilityTypes = [expected] });
        var service = new ResponsibilityTypeService(store, new FakeAuditService(), new FakeCurrentUser());

        Assert.Equal([expected], await service.GetResponsibilityTypesAsync());
        Assert.Equal(expected, await service.GetResponsibilityTypeByIdAsync(expected.Id));
        Assert.Null(await service.GetResponsibilityTypeByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateType_ValidatesNameAndDuplicate()
    {
        var existing = Type("Owner");
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { ResponsibilityTypes = [existing] });
        var service = new ResponsibilityTypeService(store, new FakeAuditService(), new FakeCurrentUser());

        Assert.Equal(ErrorType.Validation, (await service.CreateResponsibilityTypeAsync(new(" ", null))).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await service.CreateResponsibilityTypeAsync(new("owner", null))).ErrorType);
    }

    [Fact]
    public async Task CreateType_PersistsAndAudits()
    {
        var store = new InMemoryFuseStore();
        var audit = new FakeAuditService();
        var service = new ResponsibilityTypeService(store, audit, new FakeCurrentUser());

        var result = await service.CreateResponsibilityTypeAsync(new("Owner", "desc"));

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value, Assert.Single(store.Current!.ResponsibilityTypes));
        Assert.Equal(AuditAction.ResponsibilityTypeCreated, Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task UpdateType_ValidatesBlankMissingAndDuplicate()
    {
        var first = Type("First");
        var second = Type("Second");
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { ResponsibilityTypes = [first, second] });
        var service = new ResponsibilityTypeService(store, new FakeAuditService(), new FakeCurrentUser());

        Assert.Equal(ErrorType.Validation, (await service.UpdateResponsibilityTypeAsync(new(first.Id, " ", null))).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await service.UpdateResponsibilityTypeAsync(new(Guid.NewGuid(), "x", null))).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await service.UpdateResponsibilityTypeAsync(new(first.Id, "SECOND", null))).ErrorType);
    }

    [Fact]
    public async Task UpdateType_PersistsAndAudits()
    {
        var existing = Type();
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { ResponsibilityTypes = [existing] });
        var audit = new FakeAuditService();
        var service = new ResponsibilityTypeService(store, audit, new FakeCurrentUser());

        var result = await service.UpdateResponsibilityTypeAsync(new(existing.Id, "New", "changed"));

        Assert.True(result.IsSuccess);
        Assert.Equal("New", store.Current!.ResponsibilityTypes.Single().Name);
        Assert.True(result.Value!.UpdatedAt > existing.UpdatedAt);
        Assert.Equal(AuditAction.ResponsibilityTypeUpdated, Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task DeleteType_HandlesMissingReferencedAndSuccess()
    {
        var position = Position();
        var type = Type();
        var app = Application();
        var assignment = Assignment(position.Id, type.Id, app.Id);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { ResponsibilityTypes = [type], ResponsibilityAssignments = [assignment] });
        var audit = new FakeAuditService();
        var service = new ResponsibilityTypeService(store, audit, new FakeCurrentUser());

        Assert.Equal(ErrorType.NotFound, (await service.DeleteResponsibilityTypeAsync(new(Guid.NewGuid()))).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await service.DeleteResponsibilityTypeAsync(new(type.Id))).ErrorType);
        await store.UpdateAsync(s => s with { ResponsibilityAssignments = [] });
        Assert.True((await service.DeleteResponsibilityTypeAsync(new(type.Id))).IsSuccess);
        Assert.Empty(store.Current!.ResponsibilityTypes);
        Assert.Equal(AuditAction.ResponsibilityTypeDeleted, Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task AssignmentQueries_ReturnAllFilteredAndById()
    {
        var position = Position();
        var type = Type();
        var app = Application();
        var expected = Assignment(position.Id, type.Id, app.Id);
        var other = expected with { Id = Guid.NewGuid(), ApplicationId = Guid.NewGuid() };
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { ResponsibilityAssignments = [expected, other] });
        var service = new ResponsibilityAssignmentService(store, new FakeAuditService());

        Assert.Equal(2, (await service.GetResponsibilityAssignmentsAsync()).Count);
        Assert.Equal([expected], await service.GetResponsibilityAssignmentsByApplicationIdAsync(app.Id));
        Assert.Equal(expected, await service.GetResponsibilityAssignmentByIdAsync(expected.Id));
        Assert.Null(await service.GetResponsibilityAssignmentByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAssignment_ValidatesEachReferenceAndEnvironmentScope()
    {
        var position = Position();
        var type = Type();
        var app = Application();
        var environment = new EnvironmentInfo(Guid.NewGuid(), "Prod", null, []);
        var store = new InMemoryFuseStore();
        var service = new ResponsibilityAssignmentService(store, new FakeAuditService());
        var user = new FakeCurrentUser();

        Assert.Equal(ErrorType.Validation, (await service.CreateResponsibilityAssignmentAsync(Command(), user)).ErrorType);
        await store.UpdateAsync(s => s with { Positions = [position] });
        Assert.Equal(ErrorType.Validation, (await service.CreateResponsibilityAssignmentAsync(Command(), user)).ErrorType);
        await store.UpdateAsync(s => s with { ResponsibilityTypes = [type] });
        Assert.Equal(ErrorType.Validation, (await service.CreateResponsibilityAssignmentAsync(Command(), user)).ErrorType);
        await store.UpdateAsync(s => s with { Applications = [app] });
        Assert.Equal(ErrorType.Validation, (await service.CreateResponsibilityAssignmentAsync(Command(ResponsibilityScope.Environment), user)).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.CreateResponsibilityAssignmentAsync(Command(ResponsibilityScope.Environment, Guid.NewGuid()), user)).ErrorType);
        await store.UpdateAsync(s => s with { Environments = [environment] });
        Assert.True((await service.CreateResponsibilityAssignmentAsync(Command(ResponsibilityScope.Environment, environment.Id), user)).IsSuccess);

        CreateResponsibilityAssignment Command(ResponsibilityScope scope = ResponsibilityScope.All, Guid? environmentId = null) =>
            new(position.Id, type.Id, app.Id, scope, environmentId, "notes", true);
    }

    [Fact]
    public async Task CreateAssignment_PersistsAndAudits()
    {
        var position = Position();
        var type = Type();
        var app = Application();
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [position], ResponsibilityTypes = [type], Applications = [app] });
        var audit = new FakeAuditService();
        var service = new ResponsibilityAssignmentService(store, audit);

        var result = await service.CreateResponsibilityAssignmentAsync(
            new(position.Id, type.Id, app.Id, ResponsibilityScope.All, null, "notes", true), new FakeCurrentUser());

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value, Assert.Single(store.Current!.ResponsibilityAssignments));
        Assert.Equal(AuditAction.ResponsibilityAssignmentCreated, Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task UpdateAssignment_ValidatesMissingAndEachReference()
    {
        var position = Position();
        var type = Type();
        var app = Application();
        var assignment = Assignment(position.Id, type.Id, app.Id);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { ResponsibilityAssignments = [assignment] });
        var service = new ResponsibilityAssignmentService(store, new FakeAuditService());
        var user = new FakeCurrentUser();

        Assert.Equal(ErrorType.NotFound, (await service.UpdateResponsibilityAssignmentAsync(Command(Guid.NewGuid()), user)).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.UpdateResponsibilityAssignmentAsync(Command(assignment.Id), user)).ErrorType);
        await store.UpdateAsync(s => s with { Positions = [position] });
        Assert.Equal(ErrorType.Validation, (await service.UpdateResponsibilityAssignmentAsync(Command(assignment.Id), user)).ErrorType);
        await store.UpdateAsync(s => s with { ResponsibilityTypes = [type] });
        Assert.Equal(ErrorType.Validation, (await service.UpdateResponsibilityAssignmentAsync(Command(assignment.Id), user)).ErrorType);
        await store.UpdateAsync(s => s with { Applications = [app] });
        Assert.Equal(ErrorType.Validation, (await service.UpdateResponsibilityAssignmentAsync(Command(assignment.Id, ResponsibilityScope.Environment), user)).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.UpdateResponsibilityAssignmentAsync(Command(assignment.Id, ResponsibilityScope.Environment, Guid.NewGuid()), user)).ErrorType);

        UpdateResponsibilityAssignment Command(Guid id, ResponsibilityScope scope = ResponsibilityScope.All, Guid? environmentId = null) =>
            new(id, position.Id, type.Id, app.Id, scope, environmentId, "changed", false);
    }

    [Fact]
    public async Task UpdateAssignment_PersistsAndAudits()
    {
        var position = Position();
        var type = Type();
        var app = Application();
        var assignment = Assignment(position.Id, type.Id, app.Id);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [position], ResponsibilityTypes = [type], Applications = [app], ResponsibilityAssignments = [assignment] });
        var audit = new FakeAuditService();
        var service = new ResponsibilityAssignmentService(store, audit);

        var result = await service.UpdateResponsibilityAssignmentAsync(
            new(assignment.Id, position.Id, type.Id, app.Id, ResponsibilityScope.All, null, "changed", false), new FakeCurrentUser());

        Assert.True(result.IsSuccess);
        Assert.Equal("changed", store.Current!.ResponsibilityAssignments.Single().Notes);
        Assert.False(result.Value!.Primary);
        Assert.Equal(AuditAction.ResponsibilityAssignmentUpdated, Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task DeleteAssignment_HandlesMissingAndSuccess()
    {
        var assignment = Assignment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { ResponsibilityAssignments = [assignment] });
        var audit = new FakeAuditService();
        var service = new ResponsibilityAssignmentService(store, audit);

        Assert.Equal(ErrorType.NotFound, (await service.DeleteResponsibilityAssignmentAsync(new(Guid.NewGuid()), new FakeCurrentUser())).ErrorType);
        Assert.True((await service.DeleteResponsibilityAssignmentAsync(new(assignment.Id), new FakeCurrentUser())).IsSuccess);
        Assert.Empty(store.Current!.ResponsibilityAssignments);
        Assert.Equal(AuditAction.ResponsibilityAssignmentDeleted, Assert.Single(audit.Logs).Action);
    }
}
