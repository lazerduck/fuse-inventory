using Fuse.Core.Areas.Position;
using Fuse.Core.Areas.Risk;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class RiskServiceTests
{
    private static Position Position() =>
        new(Guid.NewGuid(), "Owner", null, [], DateTime.UtcNow, DateTime.UtcNow);

    private static Risk Risk(Guid ownerId, string targetType, Guid targetId) =>
        new(Guid.NewGuid(), "Risk", "description", RiskImpact.High, RiskLikelihood.Medium, RiskStatus.Identified,
            ownerId, null, targetType, targetId, "mitigation", null, null, [], "notes",
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1));

    private static Application Application(ApplicationInstance[]? instances = null) =>
        new(Guid.NewGuid(), "App", null, null, null, null, null, null, null, [], instances ?? [], [], DateTime.UtcNow, DateTime.UtcNow);

    private static RiskService Service(InMemoryFuseStore store, FakeAuditService? audit = null) =>
        new(store, new FakeTagService(store), Mock.Of<IPositionService>(), audit ?? new FakeAuditService(), new FakeCurrentUser());

    private static CreateRisk CreateCommand(Guid ownerId, string targetType, Guid targetId, Guid? approverId = null, HashSet<Guid>? tags = null) =>
        new("Risk", "description", RiskImpact.High, RiskLikelihood.Medium, RiskStatus.Identified, ownerId,
            approverId, targetType, targetId, "mitigation", DateTime.UtcNow.AddDays(1), null, tags, "notes");

    [Fact]
    public async Task Queries_ReturnAllByIdAndByTarget()
    {
        var owner = Position();
        var targetId = Guid.NewGuid();
        var expected = Risk(owner.Id, "Application", targetId);
        var other = expected with { Id = Guid.NewGuid(), TargetId = Guid.NewGuid() };
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Risks = [expected, other] });
        var service = Service(store);

        Assert.Equal(2, (await service.GetRisksAsync()).Count);
        Assert.Equal(expected, await service.GetRiskByIdAsync(expected.Id));
        Assert.Null(await service.GetRiskByIdAsync(Guid.NewGuid()));
        Assert.Equal([expected], await service.GetRisksByTargetAsync("Application", targetId));
    }

    [Fact]
    public async Task Create_RejectsMissingOwnerAndApprover()
    {
        var owner = Position();
        var app = Application();
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Applications = [app] });
        var service = Service(store);

        Assert.Equal(ErrorType.Validation, (await service.CreateRiskAsync(CreateCommand(owner.Id, "Application", app.Id))).ErrorType);
        await store.UpdateAsync(s => s with { Positions = [owner] });
        Assert.Equal(ErrorType.Validation, (await service.CreateRiskAsync(CreateCommand(owner.Id, "Application", app.Id, Guid.NewGuid()))).ErrorType);
    }

    [Theory]
    [InlineData("Application")]
    [InlineData("ApplicationInstance")]
    [InlineData("Dependency")]
    [InlineData("DataStore")]
    [InlineData("Account")]
    [InlineData("ExternalResource")]
    [InlineData("Unknown")]
    public async Task Create_RejectsMissingTargetForEveryTargetKind(string targetType)
    {
        var owner = Position();
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [owner] });

        var result = await Service(store).CreateRiskAsync(CreateCommand(owner.Id, targetType, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Create_RejectsUnknownTag()
    {
        var owner = Position();
        var app = Application();
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [owner], Applications = [app] });

        var result = await Service(store).CreateRiskAsync(CreateCommand(owner.Id, "Application", app.Id, tags: [Guid.NewGuid()]));

        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Create_PersistsAndAudits()
    {
        var owner = Position();
        var approver = Position();
        var app = Application();
        var tag = new Tag(Guid.NewGuid(), "security", null, null);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [owner, approver], Applications = [app], Tags = [tag] });
        var audit = new FakeAuditService();

        var result = await Service(store, audit).CreateRiskAsync(CreateCommand(owner.Id, "Application", app.Id, approver.Id, [tag.Id]));

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value, Assert.Single(store.Current!.Risks));
        Assert.Contains(tag.Id, result.Value!.TagIds);
        Assert.Equal(AuditAction.RiskCreated, Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task Create_AcceptsNestedApplicationInstanceAndDependencyTargets()
    {
        var owner = Position();
        var dependency = new ApplicationInstanceDependency(Guid.NewGuid(), Guid.NewGuid(), TargetKind.Application, null, DependencyAuthKind.None, null, null);
        var instance = new ApplicationInstance(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null, [dependency], [], DateTime.UtcNow, DateTime.UtcNow);
        var app = Application([instance]);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [owner], Applications = [app] });
        var service = Service(store);

        Assert.True((await service.CreateRiskAsync(CreateCommand(owner.Id, "ApplicationInstance", instance.Id))).IsSuccess);
        Assert.True((await service.CreateRiskAsync(CreateCommand(owner.Id, "Dependency", dependency.Id))).IsSuccess);
    }

    [Fact]
    public async Task Create_AcceptsNonApplicationTargetKinds()
    {
        var owner = Position();
        var dataStore = new DataStore(Guid.NewGuid(), "db", null, "Sql", Guid.NewGuid(), null, null, [], DateTime.UtcNow, DateTime.UtcNow);
        var account = new Account(Guid.NewGuid(), dataStore.Id, TargetKind.DataStore, AuthKind.None,
            new SecretBinding(SecretBindingKind.None, null, null), null, null, [], [], DateTime.UtcNow, DateTime.UtcNow);
        var external = new ExternalResource(Guid.NewGuid(), "external", null, null, [], DateTime.UtcNow, DateTime.UtcNow);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Positions = [owner], DataStores = [dataStore], Accounts = [account], ExternalResources = [external] });
        var service = Service(store);

        Assert.True((await service.CreateRiskAsync(CreateCommand(owner.Id, "DataStore", dataStore.Id))).IsSuccess);
        Assert.True((await service.CreateRiskAsync(CreateCommand(owner.Id, "Account", account.Id))).IsSuccess);
        Assert.True((await service.CreateRiskAsync(CreateCommand(owner.Id, "ExternalResource", external.Id))).IsSuccess);
    }

    [Fact]
    public async Task Update_RejectsMissingRiskAndInvalidReferences()
    {
        var owner = Position();
        var app = Application();
        var existing = Risk(owner.Id, "Application", app.Id);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Risks = [existing], Positions = [owner], Applications = [app] });
        var service = Service(store);

        Assert.Equal(ErrorType.NotFound, (await service.UpdateRiskAsync(Update(Guid.NewGuid(), owner.Id, app.Id))).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.UpdateRiskAsync(Update(existing.Id, Guid.NewGuid(), app.Id))).ErrorType);

        static UpdateRisk Update(Guid id, Guid ownerId, Guid targetId) =>
            new(id, "updated", null, RiskImpact.Low, RiskLikelihood.Low, RiskStatus.Mitigated, ownerId, null,
                "Application", targetId, null, null, null, null, null);
    }

    [Fact]
    public async Task Update_PersistsAllFieldsAndAudits()
    {
        var owner = Position();
        var app = Application();
        var existing = Risk(owner.Id, "Application", app.Id);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Risks = [existing], Positions = [owner], Applications = [app] });
        var audit = new FakeAuditService();
        var reviewDate = DateTime.UtcNow.AddDays(10);

        var result = await Service(store, audit).UpdateRiskAsync(new(existing.Id, "updated", "changed", RiskImpact.Critical,
            RiskLikelihood.High, RiskStatus.Accepted, owner.Id, null, "Application", app.Id, "new mitigation",
            reviewDate, DateTime.UtcNow, null, "new notes"));

        Assert.True(result.IsSuccess);
        Assert.Equal("updated", store.Current!.Risks.Single().Title);
        Assert.Equal(RiskImpact.Critical, result.Value!.Impact);
        Assert.True(result.Value.UpdatedAt > existing.UpdatedAt);
        Assert.Equal(AuditAction.RiskUpdated, Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task Delete_HandlesMissingAndSuccess()
    {
        var existing = Risk(Guid.NewGuid(), "Application", Guid.NewGuid());
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Risks = [existing] });
        var audit = new FakeAuditService();
        var service = Service(store, audit);

        Assert.Equal(ErrorType.NotFound, (await service.DeleteRiskAsync(new(Guid.NewGuid()))).ErrorType);
        Assert.True((await service.DeleteRiskAsync(new(existing.Id))).IsSuccess);
        Assert.Empty(store.Current!.Risks);
        Assert.Equal(AuditAction.RiskDeleted, Assert.Single(audit.Logs).Action);
    }
}
