using System.Collections.Concurrent;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

    private readonly ConcurrentDictionary<Guid, CachedSqlPermissionsOverview> _integrationCache = new();
    private readonly ConcurrentDictionary<Guid, CachedAccountSqlStatus> _accountCache = new();

    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(5);

    public SqlPermissionsCacheService(
        ILogger<SqlPermissionsCacheService> logger,
        IFuseStore store,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _store = store;
        _serviceProvider = serviceProvider;
    }

    public CachedSqlPermissionsOverview? GetCachedOverview(Guid integrationId)
    {
        _integrationCache.TryGetValue(integrationId, out var cached);
        return cached;
    }

    public CachedAccountSqlStatus? GetCachedAccountStatus(Guid accountId)
    {
        _accountCache.TryGetValue(accountId, out var cached);
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
                // Integration doesn't exist, remove from cache if present
                _integrationCache.TryRemove(integrationId, out _);
                return null;
            }

            var overview = await BuildPermissionsOverviewAsync(integration, snapshot, ct);
            if (overview is not null)
            {
                var cached = new CachedSqlPermissionsOverview(overview, DateTime.UtcNow);
                _integrationCache.AddOrUpdate(integrationId, cached, (_, _) => cached);

                // Also update account caches based on the overview
                foreach (var accountStatus in overview.Accounts)
                {
                    var accountSqlStatus = new AccountSqlStatusResponse(
                        AccountId: accountStatus.AccountId,
                        SqlIntegrationId: integration.Id,
                        SqlIntegrationName: integration.Name,
                        Status: accountStatus.Status,
                        StatusSummary: GetStatusSummary(accountStatus.Status, accountStatus.PrincipalName),
                        PermissionComparisons: accountStatus.PermissionComparisons,
                        ErrorMessage: accountStatus.ErrorMessage
                    );
                    var accountCached = new CachedAccountSqlStatus(accountSqlStatus, DateTime.UtcNow);
                    _accountCache.AddOrUpdate(accountStatus.AccountId, accountCached, (_, _) => accountCached);
                }

                return cached;
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
                _accountCache.TryRemove(accountId, out _);
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
                _accountCache.AddOrUpdate(accountId, cached, (_, _) => cached);
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
                _accountCache.AddOrUpdate(accountId, cached, (_, _) => cached);
                return cached;
            }

            var status = await BuildAccountSqlStatusAsync(account, integration, snapshot, ct);
            if (status is not null)
            {
                var cached = new CachedAccountSqlStatus(status, DateTime.UtcNow);
                _accountCache.AddOrUpdate(accountId, cached, (_, _) => cached);
                return cached;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh SQL status for account with the fuse Id {AccountId}", accountId);
        }

        return null;
    }

    public void InvalidateIntegration(Guid integrationId)
    {
        _integrationCache.TryRemove(integrationId, out _);
    }

    public void InvalidateAccount(Guid accountId)
    {
        _accountCache.TryRemove(accountId, out _);
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
    }

    private async Task<SqlIntegrationPermissionsOverviewResponse?> BuildPermissionsOverviewAsync(
        SqlIntegration integration,
        Snapshot snapshot,
        CancellationToken ct)
    {
        // Check if the integration has Read permission
        if ((integration.Permissions & SqlPermissions.Read) == 0)
        {
            return new SqlIntegrationPermissionsOverviewResponse(
                IntegrationId: integration.Id,
                IntegrationName: integration.Name,
                Accounts: Array.Empty<SqlAccountPermissionsStatus>(),
                OrphanPrincipals: Array.Empty<SqlOrphanPrincipal>(),
                Summary: new SqlPermissionsOverviewSummary(0, 0, 0, 0, 0, 0),
                ErrorMessage: "SQL integration does not have Read permission to inspect accounts.");
        }

        // Find all accounts associated with this integration's DataStore
        var associatedAccounts = snapshot.Accounts
            .Where(a => a.TargetKind == TargetKind.DataStore && a.TargetId == integration.DataStoreId)
            .ToList();

        // Get the set of principal names managed by Fuse
        var managedPrincipalNames = associatedAccounts
            .Where(a => !string.IsNullOrWhiteSpace(a.UserName))
            .Select(a => a.UserName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var accountStatuses = new List<SqlAccountPermissionsStatus>();

        foreach (var account in associatedAccounts)
        {
            var principalName = account.UserName;
            
            // Skip accounts without a username
            if (string.IsNullOrWhiteSpace(principalName))
            {
                accountStatuses.Add(new SqlAccountPermissionsStatus(
                    AccountId: account.Id,
                    AccountName: GetAccountDisplayName(account, snapshot),
                    PrincipalName: null,
                    Status: SyncStatus.NotApplicable,
                    PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                    ErrorMessage: "Account has no username configured for SQL principal mapping."));
                continue;
            }

            // Query SQL for actual permissions
            using var scope = _serviceProvider.CreateScope();
            var sqlInspector = scope.ServiceProvider.GetRequiredService<IAccountSqlInspector>();
            var (isSuccessful, actualPermissions, errorMessage) = await sqlInspector.GetPrincipalPermissionsAsync(
                integration, principalName, ct);

            if (!isSuccessful || actualPermissions is null)
            {
                accountStatuses.Add(new SqlAccountPermissionsStatus(
                    AccountId: account.Id,
                    AccountName: GetAccountDisplayName(account, snapshot),
                    PrincipalName: principalName,
                    Status: SyncStatus.Error,
                    PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                    ErrorMessage: errorMessage ?? "Failed to retrieve SQL permissions."));
                continue;
            }

            // Build permission comparisons
            var comparisons = BuildPermissionComparisons(account.Grants, actualPermissions);

            // Determine sync status
            var hasDrift = comparisons.Any(c => c.MissingPrivileges.Count > 0 || c.ExtraPrivileges.Count > 0);
            var principalMissing = !actualPermissions.Exists;
            
            SyncStatus status;
            if (principalMissing)
            {
                status = SyncStatus.MissingPrincipal;
            }
            else if (hasDrift)
            {
                status = SyncStatus.DriftDetected;
            }
            else
            {
                status = SyncStatus.InSync;
            }

            accountStatuses.Add(new SqlAccountPermissionsStatus(
                AccountId: account.Id,
                AccountName: GetAccountDisplayName(account, snapshot),
                PrincipalName: principalName,
                Status: status,
                PermissionComparisons: comparisons,
                ErrorMessage: null));
        }

        // Detect orphan principals (SQL principals not managed by any Fuse account)
        var orphanPrincipals = new List<SqlOrphanPrincipal>();
        using var scope2 = _serviceProvider.CreateScope();
        var sqlInspector2 = scope2.ServiceProvider.GetRequiredService<IAccountSqlInspector>();
        var (allPrincipalsSuccess, allPrincipals, _) = await sqlInspector2.GetAllPrincipalsAsync(integration, ct);
        
        if (allPrincipalsSuccess && allPrincipals is not null)
        {
            foreach (var principal in allPrincipals)
            {
                if (!string.IsNullOrWhiteSpace(principal.PrincipalName) && 
                    !managedPrincipalNames.Contains(principal.PrincipalName))
                {
                    orphanPrincipals.Add(new SqlOrphanPrincipal(
                        principal.PrincipalName,
                        principal.Grants.Select(g => new SqlActualGrant(g.Database, g.Schema, g.Privileges)).ToList()));
                }
            }
        }

        // Calculate summary
        var summary = new SqlPermissionsOverviewSummary(
            TotalAccounts: accountStatuses.Count,
            InSyncCount: accountStatuses.Count(s => s.Status == SyncStatus.InSync),
            DriftCount: accountStatuses.Count(s => s.Status == SyncStatus.DriftDetected),
            MissingPrincipalCount: accountStatuses.Count(s => s.Status == SyncStatus.MissingPrincipal),
            ErrorCount: accountStatuses.Count(s => s.Status == SyncStatus.Error),
            OrphanPrincipalCount: orphanPrincipals.Count
        );

        return new SqlIntegrationPermissionsOverviewResponse(
            IntegrationId: integration.Id,
            IntegrationName: integration.Name,
            Accounts: accountStatuses,
            OrphanPrincipals: orphanPrincipals,
            Summary: summary,
            ErrorMessage: null);
    }

    private async Task<AccountSqlStatusResponse?> BuildAccountSqlStatusAsync(
        Account account,
        SqlIntegration integration,
        Snapshot snapshot,
        CancellationToken ct)
    {
        // Check if the integration has Read permission
        if ((integration.Permissions & SqlPermissions.Read) == 0)
        {
            return new AccountSqlStatusResponse(
                AccountId: account.Id,
                SqlIntegrationId: integration.Id,
                SqlIntegrationName: integration.Name,
                Status: SyncStatus.Error,
                StatusSummary: "SQL integration does not have Read permission.",
                PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                ErrorMessage: "The SQL integration must have Read permission to inspect account status."
            );
        }

        // Get the principal name (username) - required for SQL inspection
        var principalName = account.UserName;
        if (string.IsNullOrWhiteSpace(principalName))
        {
            return new AccountSqlStatusResponse(
                AccountId: account.Id,
                SqlIntegrationId: integration.Id,
                SqlIntegrationName: integration.Name,
                Status: SyncStatus.NotApplicable,
                StatusSummary: "Account has no username configured for SQL principal mapping.",
                PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                ErrorMessage: null
            );
        }

        // Query SQL for actual permissions
        using var scope = _serviceProvider.CreateScope();
        var sqlInspector = scope.ServiceProvider.GetRequiredService<IAccountSqlInspector>();
        var (isSuccessful, actualPermissions, errorMessage) = await sqlInspector.GetPrincipalPermissionsAsync(
            integration, principalName, ct);

        if (!isSuccessful || actualPermissions is null)
        {
            return new AccountSqlStatusResponse(
                AccountId: account.Id,
                SqlIntegrationId: integration.Id,
                SqlIntegrationName: integration.Name,
                Status: SyncStatus.Error,
                StatusSummary: "Failed to retrieve SQL permissions.",
                PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                ErrorMessage: errorMessage ?? "Unknown error occurred while querying SQL permissions."
            );
        }

        // Build permission comparisons
        var comparisons = BuildPermissionComparisons(account.Grants, actualPermissions);

        // Determine sync status
        var hasDrift = comparisons.Any(c => c.MissingPrivileges.Count > 0 || c.ExtraPrivileges.Count > 0);
        var principalMissing = !actualPermissions.Exists;
        
        SyncStatus status;
        if (principalMissing)
        {
            status = SyncStatus.MissingPrincipal;
        }
        else if (hasDrift)
        {
            status = SyncStatus.DriftDetected;
        }
        else
        {
            status = SyncStatus.InSync;
        }

        return new AccountSqlStatusResponse(
            AccountId: account.Id,
            SqlIntegrationId: integration.Id,
            SqlIntegrationName: integration.Name,
            Status: status,
            StatusSummary: GetStatusSummary(status, principalName),
            PermissionComparisons: comparisons,
            ErrorMessage: null
        );
    }

    private static string GetStatusSummary(SyncStatus status, string? principalName) => status switch
    {
        SyncStatus.InSync => "Permissions are in sync.",
        SyncStatus.MissingPrincipal => $"SQL principal '{principalName}' does not exist.",
        SyncStatus.DriftDetected => "Permission drift detected between configured and actual grants.",
        SyncStatus.Error => "An error occurred while checking permissions.",
        SyncStatus.NotApplicable => "SQL status check is not applicable.",
        _ => "Unknown status."
    };

    private static string GetAccountDisplayName(Account account, Snapshot snapshot)
    {
        // Try to get a meaningful name from the target
        var targetName = account.TargetKind switch
        {
            TargetKind.DataStore => snapshot.DataStores.FirstOrDefault(d => d.Id == account.TargetId)?.Name,
            TargetKind.Application => snapshot.Applications.FirstOrDefault(a => a.Id == account.TargetId)?.Name,
            TargetKind.External => snapshot.ExternalResources.FirstOrDefault(e => e.Id == account.TargetId)?.Name,
            _ => null
        };

        // Build display name: username @ target or just username
        if (!string.IsNullOrWhiteSpace(account.UserName) && !string.IsNullOrWhiteSpace(targetName))
        {
            return $"{account.UserName} @ {targetName}";
        }
        
        return account.UserName ?? targetName ?? account.Id.ToString();
    }

    private static IReadOnlyList<SqlPermissionComparison> BuildPermissionComparisons(
        IReadOnlyList<Grant> configuredGrants,
        SqlPrincipalPermissions actualPermissions)
    {
        var comparisons = new List<SqlPermissionComparison>();

        // Normalize database/schema keys (null vs empty string)
        static string? NormalizeKey(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        // Group actual grants by database/schema for easier lookup
        var actualGrantsLookup = actualPermissions.Grants
            .GroupBy(g => (Database: NormalizeKey(g.Database), Schema: NormalizeKey(g.Schema)))
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(x => x.Privileges).ToHashSet()
            );

        // Process configured grants
        var processedKeys = new HashSet<(string?, string?)>();
        foreach (var configured in configuredGrants)
        {
            var key = (Database: NormalizeKey(configured.Database), Schema: NormalizeKey(configured.Schema));
            processedKeys.Add(key);

            actualGrantsLookup.TryGetValue(key, out var actualPrivileges);
            actualPrivileges ??= new HashSet<Privilege>();

            var configuredSet = configured.Privileges ?? new HashSet<Privilege>();
            var missing = configuredSet.Except(actualPrivileges).ToHashSet();
            var extra = actualPrivileges.Except(configuredSet).ToHashSet();

            comparisons.Add(new SqlPermissionComparison(
                Database: configured.Database,
                Schema: configured.Schema,
                ConfiguredPrivileges: configuredSet,
                ActualPrivileges: actualPrivileges,
                MissingPrivileges: missing,
                ExtraPrivileges: extra
            ));
        }

        // Add any actual grants that weren't in configured
        foreach (var actualKey in actualGrantsLookup.Keys)
        {
            if (!processedKeys.Contains(actualKey))
            {
                comparisons.Add(new SqlPermissionComparison(
                    Database: actualKey.Database,
                    Schema: actualKey.Schema,
                    ConfiguredPrivileges: new HashSet<Privilege>(),
                    ActualPrivileges: actualGrantsLookup[actualKey],
                    MissingPrivileges: new HashSet<Privilege>(),
                    ExtraPrivileges: actualGrantsLookup[actualKey]
                ));
            }
        }

        return comparisons;
    }
}
