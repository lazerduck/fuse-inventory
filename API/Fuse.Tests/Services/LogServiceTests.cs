using Fuse.Core.Areas.Logging;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Data.Stores;
using Xunit;

namespace Fuse.Tests.Services;

public sealed class LogServiceTests
{
    private static readonly SemaphoreSlim CreateLock = new(1, 1);

    private static async Task<string> CreateTempDir()
    {
        await CreateLock.WaitAsync();
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), $"fuse-log-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(dir);
            return dir;
        }
        finally
        {
            CreateLock.Release();
        }
    }

    [Fact]
    public async Task LogAsync_Disabled_DoesNotPersistEntries()
    {
        var settings = new AppSettings(
            Logging: new LoggingSettings
            {
                Enabled = false
            });
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbLogService(store, dir);

        await service.LogAsync(CreateLog());

        Assert.Equal(0, await service.CountAsync());
    }

    [Fact]
    public async Task QueryAsync_AppliesFiltersAndCounts()
    {
        var settings = new AppSettings(
            Logging: new LoggingSettings
            {
                Enabled = true,
                MinLevel = LogLevel.Debug
            });
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbLogService(store, dir);

        await service.LogAsync(CreateLog(LogLevel.Info, "Config", "Configuration exported", "export"));
        await service.LogAsync(CreateLog(LogLevel.Warning, "Security", "Authorization denied", "permission denied"));
        await service.LogAsync(CreateLog(LogLevel.Error, "Security", "Session token validation failed", "token"));

        var result = await service.QueryAsync(new SystemLogQuery
        {
            MinLevel = LogLevel.Warning,
            Area = "Security",
            SearchText = "denied"
        });
        var counts = await service.GetCountsAsync(new SystemLogQuery
        {
            Area = "Security"
        });

        Assert.Single(result.Logs);
        Assert.Equal("Authorization denied", result.Logs[0].Message);
        Assert.Equal(0, counts.Debug);
        Assert.Equal(0, counts.Info);
        Assert.Equal(1, counts.Warning);
        Assert.Equal(1, counts.Error);
    }

    [Fact]
    public async Task CleanupOldLogsAsync_RemovesEntriesOlderThanRetention()
    {
        var settings = new AppSettings(
            Logging: new LoggingSettings
            {
                Enabled = true,
                MinLevel = LogLevel.Debug,
                DaysToKeep = 7
            });
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbLogService(store, dir);

        await service.LogAsync(CreateLog(LogLevel.Info, "HealthCheck", "Old entry", timestamp: DateTime.UtcNow.AddDays(-30)));
        await service.LogAsync(CreateLog(LogLevel.Info, "HealthCheck", "New entry", timestamp: DateTime.UtcNow.AddDays(-1)));

        await service.CleanupOldLogsAsync();
        var result = await service.QueryAsync(new SystemLogQuery());

        Assert.Single(result.Logs);
        Assert.Equal("New entry", result.Logs[0].Message);
    }

    [Fact]
    public async Task QueryAsync_PaginatesByNewestFirstAndReturnsTotalCount()
    {
        var settings = new AppSettings(
            Logging: new LoggingSettings
            {
                Enabled = true,
                MinLevel = LogLevel.Debug
            });
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbLogService(store, dir);

        var now = DateTime.UtcNow;
        await service.LogAsync(CreateLog(LogLevel.Info, "A", "first", timestamp: now.AddMinutes(-3)));
        await service.LogAsync(CreateLog(LogLevel.Info, "A", "second", timestamp: now.AddMinutes(-2)));
        await service.LogAsync(CreateLog(LogLevel.Info, "A", "third", timestamp: now.AddMinutes(-1)));

        var firstPage = await service.QueryAsync(new SystemLogQuery { Page = 1, PageSize = 2 });
        var secondPage = await service.QueryAsync(new SystemLogQuery { Page = 2, PageSize = 2 });

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Logs.Count);
        Assert.Equal("third", firstPage.Logs[0].Message);
        Assert.Equal("second", firstPage.Logs[1].Message);

        Assert.Equal(3, secondPage.TotalCount);
        Assert.Single(secondPage.Logs);
        Assert.Equal("first", secondPage.Logs[0].Message);
    }

    [Fact]
    public async Task QueryAsync_FiltersAreaAndSearchTextCaseInsensitively()
    {
        var settings = new AppSettings(
            Logging: new LoggingSettings
            {
                Enabled = true,
                MinLevel = LogLevel.Debug
            });
        var dir = await CreateTempDir();
        var store = new FakeFuseStore(settings);
        var service = new LiteDbLogService(store, dir);

        await service.LogAsync(CreateLog(LogLevel.Warning, "Security", "Authorization denied", "permission denied"));
        await service.LogAsync(CreateLog(LogLevel.Warning, "Config", "No match", "none"));

        var result = await service.QueryAsync(new SystemLogQuery
        {
            Area = "security",
            SearchText = "DENIED"
        });

        Assert.Single(result.Logs);
        Assert.Equal("Authorization denied", result.Logs[0].Message);
    }

    private static SystemLogEntry CreateLog(LogLevel level = LogLevel.Info, string area = "Config", string message = "Entry", string? details = null, DateTime? timestamp = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Timestamp = timestamp ?? DateTime.UtcNow,
            Level = level,
            Area = area,
            Message = message,
            Details = details
        };

    private sealed class FakeFuseStore(AppSettings settings) : IFuseStore
    {
        private readonly Snapshot _snapshot = new(
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
                Array.Empty<SecurityUser>()),
            SecurityContext: new SecurityContext(
                SecurityPosture.Unrestricted,
                Array.Empty<FuseRole>(),
                Array.Empty<FuseUser>(),
                Array.Empty<FuseApiKey>(),
                Array.Empty<Session>()),
            AppSettings: settings);

        public Task<Snapshot> GetAsync(CancellationToken ct = default) => Task.FromResult(_snapshot);
        public Task<T> GetAsync<T>(Func<Snapshot, T> selector, CancellationToken ct = default) => Task.FromResult(selector(_snapshot));
        public Task<Snapshot> LoadAsync(CancellationToken ct = default) => Task.FromResult(_snapshot);
        public Task SaveAsync(Snapshot snapshot, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Func<Snapshot, Snapshot> mutate, CancellationToken ct = default)
        {
            Changed?.Invoke(_snapshot);
            return Task.CompletedTask;
        }

        public Snapshot? Current => _snapshot;
        public event Action<Snapshot>? Changed;
    }
}
