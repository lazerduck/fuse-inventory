using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;

namespace Fuse.Core.Services;

/// <summary>
/// Background service that periodically caches SQL permissions for faster load times.
/// Similar to KumaMetricsService, this runs in the background and periodically
/// refreshes the cached permissions for all SQL integrations.
/// </summary>
public class SqlPermissionsCacheService : BackgroundService, ISqlPermissionsCache
{
    private readonly ILogger<SqlPermissionsCacheService> _logger;
    private readonly IFuseStore _store;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryCache _cache;

    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(5);
    // TTL for cached entries; default to double the update interval.
    private readonly TimeSpan _ttl;

    public SqlPermissionsCacheService(
        ILogger<SqlPermissionsCacheService> logger,
        IFuseStore store,
        IServiceProvider serviceProvider,
        IMemoryCache cache)
    {
        _logger = logger;
        _store = store;
        _serviceProvider = serviceProvider;
        _cache = cache;
        _ttl = TimeSpan.FromTicks(_updateInterval.Ticks * 2);
    }

    private MemoryCacheEntryOptions CreateCacheEntryOptions(CacheItemPriority priority = CacheItemPriority.Normal) => new()
    {
        AbsoluteExpirationRelativeToNow = _ttl,
        Priority = priority
    };

    public CachedSqlPermissionsOverview? GetCachedOverview(Guid integrationId)
    {
        if (!_cache.TryGetValue(GetIntegrationMetadataKey(integrationId), out IntegrationCacheMetadata? metadata) || metadata is null)
        {
            return null;
        }

        var accountStatuses = new List<SqlAccountPermissionsStatus>();
        var orphanPrincipals = new List<SqlOrphanPrincipal>();
        var cachedAt = metadata.CachedAt;

        foreach (var (accountId, details) in metadata.Accounts)
        {
            var accountCached = GetCachedAccountStatus(accountId);
            if (accountCached is null)
            {
                return null; // Missing account cache means overview cannot be reconstructed reliably
            }

            if (accountCached.CachedAt < cachedAt)
            {
                cachedAt = accountCached.CachedAt;
            }

            accountStatuses.Add(new SqlAccountPermissionsStatus(
                AccountId: accountId,
                AccountName: details.AccountName,
                PrincipalName: details.PrincipalName,
                Status: accountCached.Status.Status,
                PermissionComparisons: accountCached.Status.PermissionComparisons,
                ErrorMessage: accountCached.Status.ErrorMessage
            ));
        }

        foreach (var principalName in metadata.OrphanPrincipals)
        {
            var orphanCached = GetCachedOrphanPrincipal(integrationId, principalName);
            if (orphanCached is null)
            {
                continue;
            }

            if (orphanCached.CachedAt < cachedAt)
            {
                cachedAt = orphanCached.CachedAt;
            }

            orphanPrincipals.Add(orphanCached.Principal);
        }

        var summary = new SqlPermissionsOverviewSummary(
            TotalAccounts: accountStatuses.Count,
            InSyncCount: accountStatuses.Count(a => a.Status == SyncStatus.InSync),
            DriftCount: accountStatuses.Count(a => a.Status == SyncStatus.DriftDetected),
            MissingPrincipalCount: accountStatuses.Count(a => a.Status == SyncStatus.MissingPrincipal),
            ErrorCount: accountStatuses.Count(a => a.Status == SyncStatus.Error),
            OrphanPrincipalCount: orphanPrincipals.Count
        );

        var overview = new SqlIntegrationPermissionsOverviewResponse(
            IntegrationId: metadata.IntegrationId,
            IntegrationName: metadata.IntegrationName,
            Accounts: accountStatuses,
            OrphanPrincipals: orphanPrincipals,
            Summary: summary,
            ErrorMessage: null
        );

        return new CachedSqlPermissionsOverview(overview, cachedAt);
    }

    public CachedAccountSqlStatus? GetCachedAccountStatus(Guid accountId)
    {
        _cache.TryGetValue(GetAccountCacheKey(accountId), out CachedAccountSqlStatus? cached);
        return cached;
    }

    public async Task<CachedSqlPermissionsOverview?> RefreshIntegrationAsync(Guid integrationId, CancellationToken ct = default)
    {
        try
        {
            var snapshot = await _store.GetAsync(ct);
            var integration = snapshot.SqlIntegrations.FirstOrDefault(s => s.Id == integrationId);
            if (integration is null)
            {
                InvalidateIntegration(integrationId);
                return null;
            }

            // Use facade to build overview
            using var scope = _serviceProvider.CreateScope();
            var inspector = scope.ServiceProvider.GetRequiredService<ISqlPermissionsInspector>();
            var overview = await inspector.GetOverviewAsync(integration, snapshot, ct);
            if (overview is not null)
            {
                _cache.TryGetValue(GetIntegrationMetadataKey(integration.Id), out IntegrationCacheMetadata? previousMetadata);
                var now = DateTime.UtcNow;
                var cacheOptions = CreateCacheEntryOptions();

                var accountMetadata = new Dictionary<Guid, AccountCacheMetadata>();
                foreach (var accountStatus in overview.Accounts)
                {
                    accountMetadata[accountStatus.AccountId] = new AccountCacheMetadata(accountStatus.AccountName, accountStatus.PrincipalName);

                    var accountSqlStatus = new AccountSqlStatusResponse(
                        AccountId: accountStatus.AccountId,
                        SqlIntegrationId: integration.Id,
                        SqlIntegrationName: integration.Name,
                        Status: accountStatus.Status,
                        StatusSummary: SqlPermissionsInspector.GetStatusSummaryStatic(accountStatus.Status, accountStatus.PrincipalName),
                        PermissionComparisons: accountStatus.PermissionComparisons,
                        ErrorMessage: accountStatus.ErrorMessage
                    );

                    var accountCached = new CachedAccountSqlStatus(accountSqlStatus, now);
                    _cache.Set(GetAccountCacheKey(accountStatus.AccountId), accountCached, cacheOptions);
                }

                var orphanPrincipals = new List<string>();
                foreach (var orphan in overview.OrphanPrincipals)
                {
                    orphanPrincipals.Add(orphan.PrincipalName);
                    _cache.Set(GetOrphanCacheKey(integration.Id, orphan.PrincipalName), new CachedOrphanPrincipal(orphan, now), cacheOptions);
                }

                if (previousMetadata is not null)
                {
                    var removedAccounts = previousMetadata.Accounts.Keys.Except(accountMetadata.Keys).ToList();
                    foreach (var removed in removedAccounts)
                    {
                        _cache.Remove(GetAccountCacheKey(removed));
                    }

                    var removedOrphans = previousMetadata.OrphanPrincipals.Except(orphanPrincipals).ToList();
                    foreach (var removed in removedOrphans)
                    {
                        _cache.Remove(GetOrphanCacheKey(integration.Id, removed));
                    }
                }

                var metadata = new IntegrationCacheMetadata(
                    IntegrationId: integration.Id,
                    IntegrationName: integration.Name,
                    Accounts: accountMetadata,
                    OrphanPrincipals: orphanPrincipals,
                    CachedAt: now);

                _cache.Set(GetIntegrationMetadataKey(integration.Id), metadata, cacheOptions);

                return new CachedSqlPermissionsOverview(overview, now);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh permissions for SQL integration {IntegrationId}", integrationId);
        }

        return null;
    }

    public async Task<CachedAccountSqlStatus?> RefreshAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        try
        {
            var snapshot = await _store.GetAsync(ct);
            var account = snapshot.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (account is null)
            {
                _cache.Remove(GetAccountCacheKey(accountId));
                return null;
            }

            // Only DataStore accounts can have SQL status
            if (account.TargetKind != TargetKind.DataStore)
            {
                var notApplicable = new AccountSqlStatusResponse(
                    AccountId: accountId,
                    SqlIntegrationId: null,
                    SqlIntegrationName: null,
                    Status: SyncStatus.NotApplicable,
                    StatusSummary: "SQL status is only available for DataStore accounts.",
                    PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                    ErrorMessage: null
                );
                var cached = new CachedAccountSqlStatus(notApplicable, DateTime.UtcNow);
                _cache.Set(GetAccountCacheKey(accountId), cached, CreateCacheEntryOptions());
                return cached;
            }

            // Find SQL integration for the DataStore
            var integration = snapshot.SqlIntegrations.FirstOrDefault(s => s.DataStoreId == account.TargetId);
            if (integration is null)
            {
                var noIntegration = new AccountSqlStatusResponse(
                    AccountId: accountId,
                    SqlIntegrationId: null,
                    SqlIntegrationName: null,
                    Status: SyncStatus.NotApplicable,
                    StatusSummary: "No SQL integration is configured for this DataStore.",
                    PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                    ErrorMessage: null
                );
                var cached = new CachedAccountSqlStatus(noIntegration, DateTime.UtcNow);
                _cache.Set(GetAccountCacheKey(accountId), cached, CreateCacheEntryOptions());
                return cached;
            }

            // Use facade to build account status
            using var scope = _serviceProvider.CreateScope();
            var inspector = scope.ServiceProvider.GetRequiredService<ISqlPermissionsInspector>();
            var status = await inspector.GetAccountStatusAsync(account, integration, snapshot, ct);
            if (status is not null)
            {
                var cached = new CachedAccountSqlStatus(status, DateTime.UtcNow);
                _cache.Set(GetAccountCacheKey(accountId), cached, CreateCacheEntryOptions());

                var accountName = GetAccountDisplayName(account, snapshot);
                UpsertIntegrationAccountMetadata(integration.Id, integration.Name, accountId, accountName, account.UserName);
                return cached;
            }
        }
        catch (Exception ex)
        {
            // The accountId is an internal Fuse identifier and does not contain sensitive information.
            _logger.LogWarning(ex, "Failed to refresh SQL status for account with the fuse Id {AccountId}", accountId);
        }

        return null;
    }

    public void InvalidateIntegration(Guid integrationId)
    {
        if (_cache.TryGetValue(GetIntegrationMetadataKey(integrationId), out IntegrationCacheMetadata? metadata) && metadata is not null)
        {
            foreach (var principal in metadata.OrphanPrincipals)
            {
                _cache.Remove(GetOrphanCacheKey(integrationId, principal));
            }
        }

        _cache.Remove(GetIntegrationMetadataKey(integrationId));
    }

    public void InvalidateAccount(Guid accountId)
    {
        _cache.Remove(GetAccountCacheKey(accountId));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SQL Permissions Cache Service starting...");

        // Wait a bit on startup to let other services initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateAllCachesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating SQL permissions caches");
            }

            await Task.Delay(_updateInterval, stoppingToken);
        }

        _logger.LogInformation("SQL Permissions Cache Service stopping.");
    }

    private async Task UpdateAllCachesAsync(CancellationToken ct)
    {
        var snapshot = await _store.GetAsync(ct);
        var integrations = snapshot.SqlIntegrations;

        if (!integrations.Any())
        {
            _logger.LogDebug("No SQL integrations configured");
            return;
        }

        _logger.LogDebug("Updating permissions cache for {Count} SQL integration(s)", integrations.Count);

        foreach (var integration in integrations)
        {
            try
            {
                await RefreshIntegrationAsync(integration.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update cache for SQL integration {IntegrationId} ({Name})", 
                    integration.Id, integration.Name);
            }
        }

        // Cleanup explicit removals: remove cached integrations/accounts that no longer exist
        var currentIntegrationIds = integrations.Select(i => i.Id).ToHashSet();
        var currentAccountIds = snapshot.Accounts.Select(a => a.Id).ToHashSet();
        // We can't iterate keys from IMemoryCache directly; rely on explicit invalidations where possible
        // For safety, remove by known keys derived from current snapshot deltas when detected elsewhere.
    }

    private CachedOrphanPrincipal? GetCachedOrphanPrincipal(Guid integrationId, string principalName)
    {
        _cache.TryGetValue(GetOrphanCacheKey(integrationId, principalName), out CachedOrphanPrincipal? cached);
        return cached;
    }

    private void UpsertIntegrationAccountMetadata(Guid integrationId, string integrationName, Guid accountId, string? accountName, string? principalName)
    {
        var cacheKey = GetIntegrationMetadataKey(integrationId);
        var options = CreateCacheEntryOptions();
        if (_cache.TryGetValue(cacheKey, out IntegrationCacheMetadata? metadata) && metadata is not null)
        {
            var accounts = new Dictionary<Guid, AccountCacheMetadata>(metadata.Accounts)
            {
                [accountId] = new AccountCacheMetadata(accountName, principalName)
            };

            var updated = metadata with
            {
                Accounts = accounts,
                IntegrationName = integrationName,
                CachedAt = DateTime.UtcNow
            };

            _cache.Set(cacheKey, updated, options);
            return;
        }

        var newMetadata = new IntegrationCacheMetadata(
            IntegrationId: integrationId,
            IntegrationName: integrationName,
            Accounts: new Dictionary<Guid, AccountCacheMetadata>
            {
                [accountId] = new AccountCacheMetadata(accountName, principalName)
            },
            OrphanPrincipals: new List<string>(),
            CachedAt: DateTime.UtcNow);

        _cache.Set(cacheKey, newMetadata, options);
    }

    private static string? GetAccountDisplayName(Account account, Snapshot snapshot)
    {
        var targetName = account.TargetKind switch
        {
            TargetKind.DataStore => snapshot.DataStores.FirstOrDefault(d => d.Id == account.TargetId)?.Name,
            TargetKind.Application => snapshot.Applications.FirstOrDefault(a => a.Id == account.TargetId)?.Name,
            TargetKind.External => snapshot.ExternalResources.FirstOrDefault(e => e.Id == account.TargetId)?.Name,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(account.UserName) && !string.IsNullOrWhiteSpace(targetName))
        {
            return $"{account.UserName} @ {targetName}";
        }

        return account.UserName ?? targetName ?? account.Id.ToString();
    }

    private static string GetIntegrationMetadataKey(Guid integrationId) => $"sql:integration:{integrationId}:meta";
    private static string GetOrphanCacheKey(Guid integrationId, string principalName) => $"sql:integration:{integrationId}:orphan:{principalName}";
    private static string GetAccountCacheKey(Guid accountId) => $"sql:account:{accountId}";

    private sealed record AccountCacheMetadata(string? AccountName, string? PrincipalName);

    private sealed record IntegrationCacheMetadata(
        Guid IntegrationId,
        string IntegrationName,
        Dictionary<Guid, AccountCacheMetadata> Accounts,
        List<string> OrphanPrincipals,
        DateTime CachedAt);

    private sealed record CachedOrphanPrincipal(SqlOrphanPrincipal Principal, DateTime CachedAt);

    // Overview and account status building delegated to ISqlPermissionsInspector facade
}
