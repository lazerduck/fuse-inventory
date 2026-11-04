using System.Text.Json;
using Fuse.Core.Areas.Activity;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Services;

public class ApplicationChangeComparisonTests
{
    private readonly EnvironmentInfo _production = new(Guid.NewGuid(), "Production", null, []);
    private readonly EnvironmentInfo _staging = new(Guid.NewGuid(), "Staging", null, []);
    private readonly ExternalResource _target = new(Guid.NewGuid(), "Billing", null, null, [], DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public void ComparesInstanceEditsAndRelationshipsWithoutDisclosingKeyMaterial()
    {
        var added = new ApplicationInstanceDependency(Guid.NewGuid(), _target.Id, TargetKind.External, 443,
            DependencyAuthKind.None, null, null);
        var removed = added with { Id = Guid.NewGuid(), TargetKind = TargetKind.DataStore, TargetId = Guid.NewGuid() };
        var old = Instance("1.0") with { Dependencies = [removed], ApiKey = new(SecretBindingKind.PlainReference, "old-secret", null) };
        var current = old with { Version = "2.0", EnvironmentId = _production.Id, Dependencies = [added],
            ApiKey = new(SecretBindingKind.PlainReference, "new-secret", null) };
        var before = App(old);
        var after = before with { Instances = [current], Description = "Receives payments" };
        var diff = Compare(before, after);

        Assert.Equal("Payments", diff.ApplicationName);
        Assert.Contains(new ApplicationChange("Description", null, "Receives payments"), diff.Groups[0].Changes);
        var group = Assert.Single(diff.Groups, g => g.InstanceId == old.Id);
        Assert.Contains("Production", group.Title);
        Assert.Contains(new ApplicationChange("Environment", "Staging", "Production"), group.Changes);
        Assert.Contains(new ApplicationChange("Deployed version", "1.0", "2.0"), group.Changes);
        Assert.Contains(group.Changes, c => c.Field == "Dependency added" && c.Before is null && c.After!.Contains("Billing"));
        Assert.Contains(group.Changes, c => c.Field == "Dependency removed" && c.After is null && c.Before!.Contains(removed.TargetId.ToString()));
        Assert.Contains(group.Changes, c => c.Field == "API key binding");
        Assert.DoesNotContain("-secret", JsonSerializer.Serialize(diff));

        var restricted = JsonSerializer.Serialize(Compare(before, after, canRead: false));
        Assert.DoesNotContain("Billing", restricted);
        Assert.DoesNotContain("Production", restricted);
        Assert.Contains(_production.Id.ToString(), restricted);
    }

    [Fact]
    public void IgnoresReorderingAndTimestampsAndMatchesInstancesById()
    {
        var dependency = new ApplicationInstanceDependency(Guid.NewGuid(), _target.Id, TargetKind.External, 443,
            DependencyAuthKind.None, null, null);
        var other = dependency with { Id = Guid.NewGuid() };
        var first = Instance("1") with { Dependencies = [dependency, other] };
        var second = Instance("2");
        var before = App(first, second);
        var reordered = before with { Instances = [second, first with { Dependencies = [other, dependency], UpdatedAt = DateTime.UtcNow }] };
        Assert.Empty(Compare(before, reordered).Groups);
        var edited = first with { Dependencies = [other, dependency with { Port = 8443 }] };
        var change = Assert.Single(Assert.Single(Compare(before, before with { Instances = [second, edited] }).Groups).Changes);
        Assert.Equal("Dependency changed", change.Field);
        Assert.Contains("Port 443", change.Before);
        Assert.Contains("Port 8443", change.After);
    }

    [Fact]
    public void DistinguishesCreationDeletionAndMissingHistory()
    {
        var app = App(Instance("1.0"));
        var created = Compare(null, app, AuditAction.ApplicationCreated);
        Assert.Contains(created.Groups, g => g.Title.StartsWith("Instance added") && g.Changes.All(c => c.Before == null));
        var deleted = Compare(app, null, AuditAction.ApplicationDeleted);
        Assert.Contains(deleted.Groups, g => g.Title.StartsWith("Instance removed") && g.Changes.All(c => c.After == null));
        var missing = Compare(null, app);
        Assert.NotNull(missing.Notice);
        Assert.Empty(missing.Groups);
    }

    private ApplicationChanges Compare(Application? before, Application? after, AuditAction action = AuditAction.ApplicationUpdated,
        bool canRead = true)
    {
        var snapshot = new InMemoryFuseStore().Current! with { Environments = [_staging, _production], ExternalResources = [_target] };
        var version = new EntityVersion { EntityId = (after ?? before)!.Id, Action = action, EntitySnapshot = EntityExtractor.SerializeEntity(after) };
        return new ApplicationChangeComparison(snapshot, _ => canRead).Compare(version, EntityExtractor.SerializeEntity(before));
    }

    private ApplicationInstance Instance(string version) =>
        new(Guid.NewGuid(), _staging.Id, null, null, null, null, version, [], [], DateTime.UtcNow, DateTime.UtcNow);

    private static Application App(params ApplicationInstance[] instances) =>
        new(Guid.NewGuid(), "Payments", null, null, null, null, null, null, null, [], instances, [], DateTime.UtcNow, DateTime.UtcNow);
}
