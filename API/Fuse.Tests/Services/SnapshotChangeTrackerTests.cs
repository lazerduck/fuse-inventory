using Fuse.Core.Areas.Activity;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class SnapshotChangeTrackerTests
{
    [Fact]
    public async Task Changes_CreateUpdateAndDeleteVersionHistoryWithUsefulDescriptions()
    {
        var history = new Mock<IVersionHistoryService>();
        var saved = new List<EntityVersion>();
        history.Setup(x => x.GetLatestVersionAsync(It.IsAny<Guid>(), It.IsAny<EntityType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => saved.LastOrDefault());
        history.Setup(x => x.SaveVersionAsync(It.IsAny<EntityVersion>(), It.IsAny<CancellationToken>()))
            .Callback<EntityVersion, CancellationToken>((version, _) => saved.Add(version))
            .Returns(Task.CompletedTask);
        var tracker = new SnapshotChangeTracker(history.Object);
        var initial = new InMemoryFuseStore().Current!;
        tracker.Initialize(initial);
        var tagId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SnapshotChangeTracker.SetUserContext("alice", userId);

        var created = initial with { Tags = [new Tag(tagId, "Production", "Important", TagColor.Red)] };
        await tracker.OnSnapshotChangedAsync(created);
        var updated = created with { Tags = [created.Tags[0] with { Name = "Critical", Description = "Urgent" }] };
        await tracker.OnSnapshotChangedAsync(updated);
        await tracker.OnSnapshotChangedAsync(updated with { Tags = [] });

        Assert.Collection(saved,
            version =>
            {
                Assert.Equal((EntityType.Tag, AuditAction.TagCreated, 1), (version.EntityType, version.Action, version.Version));
                Assert.Equal(tagId, version.EntityId);
                Assert.Equal("alice", version.UserName);
                Assert.Equal(userId, version.UserId);
                Assert.Contains("Created Tag 'Production'", version.ChangeDescription);
                Assert.Contains("Production", version.EntitySnapshot);
            },
            version =>
            {
                Assert.Equal((AuditAction.TagUpdated, 2), (version.Action, version.Version));
                Assert.Equal("Updated Tag 'Critical': changed name, description", version.ChangeDescription);
                Assert.Contains("Critical", version.EntitySnapshot);
            },
            version =>
            {
                Assert.Equal((AuditAction.TagDeleted, 3), (version.Action, version.Version));
                Assert.Equal($"Deleted Tag {tagId}", version.ChangeDescription);
                Assert.Null(version.EntitySnapshot);
            });
    }

    [Fact]
    public async Task UnchangedEntitiesAndTimestampOnlyChangesDoNotCreateMisleadingDescriptions()
    {
        var history = new Mock<IVersionHistoryService>();
        var saved = new List<EntityVersion>();
        history.Setup(x => x.GetLatestVersionAsync(It.IsAny<Guid>(), It.IsAny<EntityType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityVersion?)null);
        history.Setup(x => x.SaveVersionAsync(It.IsAny<EntityVersion>(), It.IsAny<CancellationToken>()))
            .Callback<EntityVersion, CancellationToken>((version, _) => saved.Add(version))
            .Returns(Task.CompletedTask);
        var tracker = new SnapshotChangeTracker(history.Object);
        var initial = new InMemoryFuseStore().Current!;
        var role = new Role(Guid.NewGuid(), "Operators", "", [], DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1));
        var withRole = initial with { Security = initial.Security with { Roles = [role] } };
        tracker.Initialize(withRole);

        await tracker.OnSnapshotChangedAsync(withRole);
        await tracker.OnSnapshotChangedAsync(withRole with
        {
            Security = withRole.Security with { Roles = [role with { UpdatedAt = DateTime.UtcNow }] }
        });

        var version = Assert.Single(saved);
        Assert.Equal(AuditAction.RoleUpdated, version.Action);
        Assert.Equal("Updated SecurityRole 'Operators'", version.ChangeDescription);
    }

    [Fact]
    public async Task FirstSnapshotOnlyInitializesTrackerAndSystemIsTheDefaultActor()
    {
        var history = new Mock<IVersionHistoryService>();
        history.Setup(x => x.GetLatestVersionAsync(It.IsAny<Guid>(), It.IsAny<EntityType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityVersion?)null);
        EntityVersion? saved = null;
        history.Setup(x => x.SaveVersionAsync(It.IsAny<EntityVersion>(), It.IsAny<CancellationToken>()))
            .Callback<EntityVersion, CancellationToken>((version, _) => saved = version)
            .Returns(Task.CompletedTask);
        var tracker = new SnapshotChangeTracker(history.Object);
        var initial = new InMemoryFuseStore().Current!;

        await tracker.OnSnapshotChangedAsync(initial);
        history.Verify(x => x.SaveVersionAsync(It.IsAny<EntityVersion>(), It.IsAny<CancellationToken>()), Times.Never);

        SnapshotChangeTracker.SetUserContext("System", null);
        await tracker.OnSnapshotChangedAsync(initial with { Tags = [new Tag(Guid.NewGuid(), "New", null, null)] });

        Assert.NotNull(saved);
        Assert.Equal("System", saved.UserName);
        Assert.Null(saved.UserId);
    }

    [Fact]
    public async Task FailedTrackingStillAdvancesBaselineToAvoidDuplicatingTheSameChange()
    {
        var history = new Mock<IVersionHistoryService>();
        history.Setup(x => x.GetLatestVersionAsync(It.IsAny<Guid>(), It.IsAny<EntityType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));
        var tracker = new SnapshotChangeTracker(history.Object);
        var initial = new InMemoryFuseStore().Current!;
        tracker.Initialize(initial);
        var changed = initial with { Tags = [new Tag(Guid.NewGuid(), "Tag", null, null)] };

        await Assert.ThrowsAsync<InvalidOperationException>(() => tracker.OnSnapshotChangedAsync(changed));
        await tracker.OnSnapshotChangedAsync(changed);

        history.Verify(x => x.GetLatestVersionAsync(It.IsAny<Guid>(), EntityType.Tag, It.IsAny<CancellationToken>()), Times.Once);
    }
}
