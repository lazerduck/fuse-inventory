using Fuse.Core.Areas.Activity;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Xunit;

namespace Fuse.Tests.Services;

public class ActivityFeedServiceTests
{
    [Fact]
    public async Task QueryAsync_ReturnsItems_WithUndoFlags()
    {
        var entityId = Guid.NewGuid();

        var deleteVersion = CreateVersion(entityId, EntityType.Application, 3, null, AuditAction.ApplicationDeleted, "alice");
        var updateVersion = CreateVersion(entityId, EntityType.Application, 2, "{\"id\":\"x\"}", AuditAction.ApplicationUpdated, "alice");
        var createVersion = CreateVersion(entityId, EntityType.Application, 1, "{\"id\":\"x\"}", AuditAction.ApplicationCreated, "alice");

        var versionService = new InMemoryVersionHistoryService(new[] { deleteVersion, updateVersion, createVersion });
        var service = new ActivityFeedService(versionService);

        var result = await service.QueryByEntityAsync(EntityType.Application, entityId);

        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, i => Assert.Equal(EntityType.Application, i.EntityType));

        var deleteItem = result.Items.Single(i => i.Version == 3);
        var createItem = result.Items.Single(i => i.Version == 1);

        Assert.True(deleteItem.CanUndo);
        Assert.True(createItem.CanUndo);
    }

    private static EntityVersion CreateVersion(
        Guid entityId,
        EntityType type,
        int version,
        string? snapshot,
        AuditAction action,
        string user)
    {
        return new EntityVersion(
            id: Guid.NewGuid(),
            entityId: entityId,
            entityType: type,
            version: version,
            entitySnapshot: snapshot,
            timestamp: DateTime.UtcNow.AddMinutes(version),
            action: action,
            userName: user,
            userId: Guid.NewGuid(),
            changeDescription: null);
    }

    private sealed class InMemoryVersionHistoryService : IVersionHistoryService
    {
        private readonly List<EntityVersion> _versions;

        public InMemoryVersionHistoryService(IEnumerable<EntityVersion> versions)
        {
            _versions = versions.ToList();
        }

        public Task SaveVersionAsync(EntityVersion version, CancellationToken ct = default)
        {
            _versions.Add(version);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EntityVersion>> GetVersionsAsync(Guid entityId, EntityType entityType, int? limit = null, CancellationToken ct = default)
        {
            IEnumerable<EntityVersion> query = _versions
                .Where(v => v.EntityId == entityId && v.EntityType == entityType)
                .OrderByDescending(v => v.Version);

            if (limit.HasValue)
                query = query.Take(limit.Value);

            return Task.FromResult((IReadOnlyList<EntityVersion>)query.ToList());
        }

        public Task<VersionHistoryResult> QueryAsync(VersionHistoryQuery query, CancellationToken ct = default)
        {
            IEnumerable<EntityVersion> filtered = _versions;

            if (query.EntityId.HasValue)
                filtered = filtered.Where(v => v.EntityId == query.EntityId.Value);
            if (query.EntityType.HasValue)
                filtered = filtered.Where(v => v.EntityType == query.EntityType.Value);

            var ordered = filtered.OrderByDescending(v => v.Timestamp).ToList();
            return Task.FromResult(new VersionHistoryResult(ordered, ordered.Count, query.Page, query.PageSize));
        }

        public Task<EntityVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken ct = default)
            => Task.FromResult(_versions.FirstOrDefault(v => v.Id == versionId));

        public Task<EntityVersion?> GetLatestVersionAsync(Guid entityId, EntityType entityType, CancellationToken ct = default)
            => Task.FromResult(_versions.Where(v => v.EntityId == entityId && v.EntityType == entityType).OrderByDescending(v => v.Version).FirstOrDefault());

        public Task PruneOldVersionsAsync(Guid entityId, EntityType entityType, int keepCount, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task PruneAllOldVersionsAsync(int keepCount, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
