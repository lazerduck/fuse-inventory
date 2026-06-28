using Fuse.Core.Models;
using Fuse.Core.Interfaces;
using Fuse.Data.Stores;
using Xunit;

namespace Fuse.Tests.Services;

public class AuditLogRetentionTests
{
    private static readonly SemaphoreSlim CreateLock = new(1, 1);

    private static async Task<string> CreateTempDir()
    {
        await CreateLock.WaitAsync();
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), $"fuse-audit-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(dir);
            return dir;
        }
        finally
        {
            CreateLock.Release();
        }
    }

    [Fact]
    public async Task CleanupOldAuditLogs_AuditLogDaysNull_NoCleanup()
    {
        var settings = new AppSettings(
            IncompleteDataWarningEnabled: true,
            LocalLicenseValidationOnly: false,
            HideValidLicenseChip: false,
            AuditLogDaysToKeep: null
        );
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbAuditService(store, dir);

        await service.LogAsync(CreateLog(DateTime.UtcNow.AddYears(-1)));
        await service.LogAsync(CreateLog(DateTime.UtcNow.AddHours(-1)));
        await service.LogAsync(CreateLog(DateTime.UtcNow.AddYears(-1)));

        await service.CleanupOldAuditLogsAsync();

        var result = await service.QueryAsync(new AuditLogQuery(), CancellationToken.None);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task CleanupOldAuditLogs_AuditLogDaysZero_NoCleanup()
    {
        var settings = new AppSettings(
            IncompleteDataWarningEnabled: true,
            LocalLicenseValidationOnly: false,
            HideValidLicenseChip: false,
            AuditLogDaysToKeep: 0
        );
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbAuditService(store, dir);

        await service.LogAsync(CreateLog(DateTime.UtcNow.AddYears(-1)));
        await service.LogAsync(CreateLog(DateTime.UtcNow.AddHours(-1)));
        await service.LogAsync(CreateLog(DateTime.UtcNow.AddYears(-1)));

        await service.CleanupOldAuditLogsAsync();

        var result = await service.QueryAsync(new AuditLogQuery(), CancellationToken.None);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task CleanupOldAuditLogs_AuditLogDaysLimited_RemovesOld()
    {
        var settings = new AppSettings(
            IncompleteDataWarningEnabled: true,
            LocalLicenseValidationOnly: false,
            HideValidLicenseChip: false,
            AuditLogDaysToKeep: 7
        );
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbAuditService(store, dir);

        await service.LogAsync(CreateLog(DateTime.UtcNow.AddDays(-30), AuditArea.Application, AuditAction.ApplicationCreated, "alice"));
        await service.LogAsync(CreateLog(DateTime.UtcNow.AddDays(-1), AuditArea.Application, AuditAction.ApplicationUpdated, "bob"));
        await service.LogAsync(CreateLog(DateTime.UtcNow, AuditArea.Application, AuditAction.ApplicationDeleted, "charlie"));

        await service.CleanupOldAuditLogsAsync();

        var result = await service.QueryAsync(new AuditLogQuery(), CancellationToken.None);
        Assert.Equal(2, result.TotalCount);

        var logs = result.Logs.OrderBy(l => l.UserName).ToList();
        Assert.Contains(logs, l => l.UserName == "bob");
        Assert.Contains(logs, l => l.UserName == "charlie");
        Assert.DoesNotContain(logs, l => l.UserName == "alice");
    }

    [Fact]
    public async Task CleanupOldAuditLogs_AuditLogDaysZero_DoesNothing()
    {
        var settings = new AppSettings(
            IncompleteDataWarningEnabled: true,
            LocalLicenseValidationOnly: false,
            HideValidLicenseChip: false,
            AuditLogDaysToKeep: 0
        );
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbAuditService(store, dir);

        await service.LogAsync(CreateLog(DateTime.UtcNow.AddDays(-5)));
        await service.LogAsync(CreateLog(DateTime.UtcNow));

        await service.CleanupOldAuditLogsAsync();

        var result = await service.QueryAsync(new AuditLogQuery(), CancellationToken.None);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesSpecifiedRange()
    {
        var settings = new AppSettings();
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbAuditService(store, dir);

        await service.LogAsync(CreateLog(DateTime.UtcNow.AddDays(-30)));
        await service.LogAsync(CreateLog(DateTime.UtcNow.AddDays(-1)));
        await service.LogAsync(CreateLog(DateTime.UtcNow.AddDays(-30)));

        await service.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-2));

        var result = await service.QueryAsync(new AuditLogQuery(), CancellationToken.None);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task LogAsync_AddsLogCorrectly()
    {
        var settings = new AppSettings();
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbAuditService(store, dir);

        var entityId = Guid.NewGuid();
        var expectedLog = new AuditLog(
            id: entityId,
            timestamp: DateTime.UtcNow,
            action: AuditAction.ApplicationCreated,
            area: AuditArea.Application,
            userName: "tester",
            userId: Guid.Empty,
            entityId: entityId,
            changeDetails: "Created Application"
        );

        await service.LogAsync(expectedLog);

        var result = await service.QueryAsync(new AuditLogQuery { UserName = "tester" }, CancellationToken.None);
        Assert.Equal(1, result.TotalCount);
        var log = result.Logs.First();
        Assert.Equal(expectedLog.Id, log.Id);
        Assert.Equal(AuditAction.ApplicationCreated, log.Action);
        Assert.Equal("Created Application", log.ChangeDetails);
    }

    [Fact]
    public async Task CleanupOldAuditLogs_CleanupDoesNotThrowWithZero()
    {
        var settings = new AppSettings(
            IncompleteDataWarningEnabled: true,
            LocalLicenseValidationOnly: false,
            HideValidLicenseChip: false,
            AuditLogDaysToKeep: 0
        );
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbAuditService(store, dir);

        await service.CleanupOldAuditLogsAsync();

        var result = await service.QueryAsync(new AuditLogQuery(), CancellationToken.None);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task CleanupOldAuditLogs_AuditLogDaysOne_DoesNotRemoveToday()
    {
        // If AuditLogDaysToKeep = 1, logs from today should still remain
        var settings = new AppSettings(
            IncompleteDataWarningEnabled: true,
            LocalLicenseValidationOnly: false,
            HideValidLicenseChip: false,
            AuditLogDaysToKeep: 1
        );
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbAuditService(store, dir);

        var today = new AuditLog(
            id: Guid.NewGuid(),
            timestamp: DateTime.UtcNow,
            action: AuditAction.ApplicationCreated,
            area: AuditArea.Application,
            userName: "alice",
            userId: Guid.Empty,
            entityId: Guid.NewGuid(),
            changeDetails: "Today"
        );
        var yesterday = new AuditLog(
            id: Guid.NewGuid(),
            timestamp: DateTime.UtcNow.AddHours(-23),
            action: AuditAction.ApplicationUpdated,
            area: AuditArea.Application,
            userName: "bob",
            userId: Guid.Empty,
            entityId: Guid.NewGuid(),
            changeDetails: "Yesterday"
        );
        var twoDaysAgo = new AuditLog(
            id: Guid.NewGuid(),
            timestamp: DateTime.UtcNow.AddHours(-49),
            action: AuditAction.ApplicationDeleted,
            area: AuditArea.Application,
            userName: "charlie",
            userId: Guid.Empty,
            entityId: Guid.NewGuid(),
            changeDetails: "Two days ago"
        );

        await service.LogAsync(today);
        await service.LogAsync(yesterday);
        await service.LogAsync(twoDaysAgo);

        await service.CleanupOldAuditLogsAsync();

        var result = await service.QueryAsync(new AuditLogQuery(), CancellationToken.None);
        // "today" (now) + "yesterday" (23h ago, within 1 day of now) should remain
        // "two days ago" (49h ago) should be removed
        Assert.Equal(2, result.TotalCount);
    }

    private static AuditLog CreateLog(DateTime timestamp, AuditArea area = AuditArea.Application, AuditAction action = AuditAction.ApplicationCreated, string user = "tester")
    {
        return new AuditLog(
            id: Guid.NewGuid(),
            timestamp: timestamp,
            action: action,
            area: area,
            userName: user,
            userId: Guid.Empty,
            entityId: Guid.NewGuid(),
            changeDetails: $"Audit event at {timestamp}"
        );
    }

    private sealed class FakeFuseStore : IFuseStore
    {
        private AppSettings _settings;
        private readonly Snapshot _snapshot;

        public FakeFuseStore(AppSettings settings)
        {
            _settings = settings;
            _snapshot = new Snapshot(
                Applications: Array.Empty<Application>(),
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
                Security: new SecurityState(
                    new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow),
                    Array.Empty<SecurityUser>()
                ),
                SecurityContext: new SecurityContext(
                    SecurityPosture.Unrestricted,
                    Array.Empty<FuseRole>(),
                    Array.Empty<FuseUser>(),
                    Array.Empty<FuseApiKey>(),
                    Array.Empty<Session>()
                ),
                AppSettings: settings
            );
        }

        public Task<Snapshot> GetAsync(CancellationToken ct = default)
            => Task.FromResult(_snapshot);

        public Task<T> GetAsync<T>(Func<Snapshot, T> selector, CancellationToken ct = default)
            => Task.FromResult(selector(_snapshot));

        public Task<Snapshot> LoadAsync(CancellationToken ct = default)
            => Task.FromResult(_snapshot);

        public Task SaveAsync(Snapshot snapshot, CancellationToken ct = default)
        {
            _settings = snapshot.AppSettings;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Func<Snapshot, Snapshot> mutate, CancellationToken ct = default)
        {
            _settings = mutate(_snapshot).AppSettings;
            Changed?.Invoke(_snapshot);
            return Task.CompletedTask;
        }

        public Snapshot? Current => _snapshot;
        public event Action<Snapshot>? Changed;
    }
}