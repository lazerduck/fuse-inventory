using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Services;

public class UndoServiceTests
{
    [Fact]
    public async Task UndoChangeAsync_WhenVersionMissing_ReturnsNotFound()
    {
        var store = new InMemoryFuseStore();
        var versionService = new InMemoryVersionHistoryService();
        var audit = new FakeAuditService();
        var service = new UndoService(versionService, store, audit, new FakeCurrentUser());

        var result = await service.UndoChangeAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UndoChangeAsync_RestoresPreviousVersion_AndLogsAudit()
    {
        var appId = Guid.NewGuid();
        var v1 = CreateApp(appId, "v1");
        var v2 = CreateApp(appId, "v2");

        var snapshot = new Snapshot(
            Applications: new[] { v2 },
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Identities: Array.Empty<Identity>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: Array.Empty<SqlIntegration>(),
            Positions: Array.Empty<Position>(),
            ResponsibilityTypes: Array.Empty<ResponsibilityType>(),
            ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
            Risks: Array.Empty<Risk>(),
            MessageBrokers: Array.Empty<MessageBroker>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        var store = new InMemoryFuseStore(snapshot);
        var versionService = new InMemoryVersionHistoryService();

        var v1Entry = CreateVersion(appId, EntityType.Application, 1, EntityExtractor.SerializeEntity(v1), AuditAction.ApplicationCreated);
        var v2Entry = CreateVersion(appId, EntityType.Application, 2, EntityExtractor.SerializeEntity(v2), AuditAction.ApplicationUpdated);
        versionService.Add(v1Entry);
        versionService.Add(v2Entry);

        var audit = new FakeAuditService();
        var service = new UndoService(versionService, store, audit, new FakeCurrentUser());

        var result = await service.UndoChangeAsync(v2Entry.Id);

        Assert.True(result.IsSuccess);
        var current = await store.GetAsync();
        var app = current.Applications.Single(a => a.Id == appId);
        Assert.Equal("v1", app.Version);
        Assert.Contains(audit.Logs, l => l.Action == AuditAction.ChangeReverted && l.EntityId == appId);
    }

    private static Application CreateApp(Guid id, string version)
    {
        return new Application(
            Id: id,
            Name: "Payments",
            Version: version,
            Description: null,
            Owner: null,
            Notes: null,
            Framework: "dotnet",
            RepositoryUri: null,
            Icon: null,
            TagIds: new HashSet<Guid>(),
            Instances: Array.Empty<ApplicationInstance>(),
            Pipelines: Array.Empty<ApplicationPipeline>(),
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );
    }

    private static EntityVersion CreateVersion(Guid entityId, EntityType type, int version, string? snapshot, AuditAction action)
    {
        return new EntityVersion(
            id: Guid.NewGuid(),
            entityId: entityId,
            entityType: type,
            version: version,
            entitySnapshot: snapshot,
            timestamp: DateTime.UtcNow,
            action: action,
            userName: "tester",
            userId: Guid.NewGuid(),
            changeDescription: null);
    }

    private sealed class InMemoryVersionHistoryService : IVersionHistoryService
    {
        private readonly List<EntityVersion> _versions = new();

        public void Add(EntityVersion version) => _versions.Add(version);

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
    }
}
