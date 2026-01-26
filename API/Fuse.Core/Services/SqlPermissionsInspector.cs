using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Services;

public class SqlPermissionsInspector : ISqlPermissionsInspector
{
    private readonly IAccountSqlInspector _sqlInspector;

    public SqlPermissionsInspector(IAccountSqlInspector sqlInspector)
    {
        _sqlInspector = sqlInspector;
    }

    public async Task<AccountSqlStatusResponse> GetAccountStatusAsync(
        Account account,
        SqlIntegration integration,
        Snapshot snapshot,
        CancellationToken ct = default)
    {
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

        var (isSuccessful, actualPermissions, errorMessage) = await _sqlInspector.GetPrincipalPermissionsAsync(
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

        var comparisons = SqlPermissionDiff.BuildComparisons(account.Grants, actualPermissions);
        var status = SqlPermissionDiff.ComputeStatus(actualPermissions, comparisons);

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

    public async Task<SqlIntegrationPermissionsOverviewResponse> GetOverviewAsync(
        SqlIntegration integration,
        Snapshot snapshot,
        CancellationToken ct = default)
    {
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

        var associatedAccounts = snapshot.Accounts
            .Where(a => a.TargetKind == TargetKind.DataStore && a.TargetId == integration.DataStoreId)
            .ToList();

        var managedPrincipalNames = associatedAccounts
            .Where(a => !string.IsNullOrWhiteSpace(a.UserName))
            .Select(a => a.UserName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        const int maxConcurrency = 4;
        var accountStatuses = new List<SqlAccountPermissionsStatus>();
        var throttler = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var accountTasks = associatedAccounts.Select(async account =>
        {
            await throttler.WaitAsync(ct);
            try
            {
                var principalName = account.UserName;
                if (string.IsNullOrWhiteSpace(principalName))
                {
                    return new SqlAccountPermissionsStatus(
                        AccountId: account.Id,
                        AccountName: GetAccountDisplayName(account, snapshot),
                        PrincipalName: null,
                        Status: SyncStatus.NotApplicable,
                        PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                        ErrorMessage: "Account has no username configured for SQL principal mapping.");
                }

                var (isSuccessful, actualPermissions, errorMessage) = await _sqlInspector.GetPrincipalPermissionsAsync(
                    integration, principalName, ct);

                if (!isSuccessful || actualPermissions is null)
                {
                    return new SqlAccountPermissionsStatus(
                        AccountId: account.Id,
                        AccountName: GetAccountDisplayName(account, snapshot),
                        PrincipalName: principalName,
                        Status: SyncStatus.Error,
                        PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                        ErrorMessage: errorMessage ?? "Failed to retrieve SQL permissions.");
                }

                var comparisons = SqlPermissionDiff.BuildComparisons(account.Grants, actualPermissions);
                var status = SqlPermissionDiff.ComputeStatus(actualPermissions, comparisons);

                return new SqlAccountPermissionsStatus(
                    AccountId: account.Id,
                    AccountName: GetAccountDisplayName(account, snapshot),
                    PrincipalName: principalName,
                    Status: status,
                    PermissionComparisons: comparisons,
                    ErrorMessage: null);
            }
            finally
            {
                throttler.Release();
            }
        }).ToList();

        accountStatuses.AddRange(await Task.WhenAll(accountTasks));

        var orphanPrincipals = new List<SqlOrphanPrincipal>();
        var (principalNamesSuccess, principalNames, _) = await _sqlInspector.GetAllPrincipalNamesAsync(integration, ct);
        if (principalNamesSuccess && principalNames is not null)
        {
            var orphanNames = principalNames
                .Where(name => !string.IsNullOrWhiteSpace(name) && !managedPrincipalNames.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var orphanTasks = orphanNames.Select(async orphanName =>
            {
                await throttler.WaitAsync(ct);
                try
                {
                    var (isSuccessful, permissions, _) = await _sqlInspector.GetPrincipalPermissionsAsync(
                        integration, orphanName, ct);
                    if (!isSuccessful || permissions is null || !permissions.Exists)
                    {
                        return null;
                    }

                    return new SqlOrphanPrincipal(
                        orphanName,
                        permissions.Grants.Select(g => new SqlActualGrant(g.Database, g.Schema, g.Privileges)).ToList());
                }
                finally
                {
                    throttler.Release();
                }
            }).ToList();

            var orphanResults = await Task.WhenAll(orphanTasks);
            orphanPrincipals.AddRange(orphanResults.Where(o => o is not null)!);
        }

        var summary = new SqlPermissionsOverviewSummary(
            TotalAccounts: accountStatuses.Count,
            InSyncCount: accountStatuses.Count(s => s.Status == SyncStatus.InSync),
            DriftCount: accountStatuses.Count(s => s.Status == SyncStatus.DriftDetected),
            MissingPrincipalCount: accountStatuses.Count(s => s.Status == SyncStatus.MissingPrincipal),
            ErrorCount: accountStatuses.Count(s => s.Status == SyncStatus.Error),
            OrphanPrincipalCount: orphanPrincipals.Count);

        return new SqlIntegrationPermissionsOverviewResponse(
            IntegrationId: integration.Id,
            IntegrationName: integration.Name,
            Accounts: accountStatuses,
            OrphanPrincipals: orphanPrincipals,
            Summary: summary,
            ErrorMessage: null);
    }

    private static string GetAccountDisplayName(Account account, Snapshot snapshot)
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

    private static string GetStatusSummary(SyncStatus status, string? principalName) => status switch
    {
        SyncStatus.InSync => "Permissions are in sync.",
        SyncStatus.MissingPrincipal => $"SQL principal '{principalName}' does not exist.",
        SyncStatus.DriftDetected => "Permission drift detected between configured and actual grants.",
        SyncStatus.Error => "An error occurred while checking permissions.",
        SyncStatus.NotApplicable => "SQL status check is not applicable.",
        _ => "Unknown status."
    };

    /// <summary>
    /// Public static helper for external callers that need to generate status summaries.
    /// </summary>
    public static string GetStatusSummaryStatic(SyncStatus status, string? principalName) => status switch
    {
        SyncStatus.InSync => "Permissions are in sync.",
        SyncStatus.MissingPrincipal => $"SQL principal '{principalName}' does not exist.",
        SyncStatus.DriftDetected => "Permission drift detected between configured and actual grants.",
        SyncStatus.Error => "An error occurred while checking permissions.",
        SyncStatus.NotApplicable => "SQL status check is not applicable.",
        _ => "Unknown status."
    };
}
