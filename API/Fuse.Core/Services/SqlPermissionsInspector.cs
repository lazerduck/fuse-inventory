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

        // Collect all managed principal names for batch query
        var managedPrincipalList = associatedAccounts
            .Where(a => !string.IsNullOrWhiteSpace(a.UserName))
            .Select(a => a.UserName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Batch query: single SQL call fetches all principals' permissions at once
        // instead of N individual calls (one per principal). Each database is scanned
        // ONCE for all principals instead of N times.
        IReadOnlyDictionary<string, SqlPrincipalPermissions> permissionsMap = new Dictionary<string, SqlPrincipalPermissions>(StringComparer.OrdinalIgnoreCase);
        string? batchError = null;

        if (managedPrincipalList.Count > 0)
        {
            var (batchOk, batchMap, batchMsg) = await _sqlInspector.GetPrincipalPermissionsBatchAsync(
                integration, managedPrincipalList, ct);

            if (batchOk)
            {
                permissionsMap = batchMap;
            }
            else
            {
                batchError = batchMsg ?? "Failed to retrieve SQL permissions.";
            }
        }

        // Build account statuses from the batch result
        var accountStatuses = new List<SqlAccountPermissionsStatus>();

        foreach (var account in associatedAccounts)
        {
            var principalName = account.UserName;
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

            if (permissionsMap.TryGetValue(principalName, out var actualPermissions) && actualPermissions != null)
            {
                // If batch query succeeded for this principal, use it
                if (actualPermissions.Exists)
                {
                    var comparisons = SqlPermissionDiff.BuildComparisons(account.Grants, actualPermissions);
                    var status = SqlPermissionDiff.ComputeStatus(actualPermissions, comparisons);

                    accountStatuses.Add(new SqlAccountPermissionsStatus(
                        AccountId: account.Id,
                        AccountName: GetAccountDisplayName(account, snapshot),
                        PrincipalName: principalName,
                        Status: status,
                        PermissionComparisons: comparisons,
                        ErrorMessage: null));
                }
                else
                {
                    // Principal doesn't exist in SQL
                    accountStatuses.Add(new SqlAccountPermissionsStatus(
                        AccountId: account.Id,
                        AccountName: GetAccountDisplayName(account, snapshot),
                        PrincipalName: principalName,
                        Status: SyncStatus.MissingPrincipal,
                        PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                        ErrorMessage: null));
                }
            }
            else if (batchError != null)
            {
                // Batch query failed for this principal
                accountStatuses.Add(new SqlAccountPermissionsStatus(
                    AccountId: account.Id,
                    AccountName: GetAccountDisplayName(account, snapshot),
                    PrincipalName: principalName,
                    Status: SyncStatus.Error,
                    PermissionComparisons: Array.Empty<SqlPermissionComparison>(),
                    ErrorMessage: batchError));
            }
        }

        // Orphan principals: query all SQL principals, then find those not in managedPrincipalNames
        // Also use batch query for orphans to keep it efficient
        var orphanPrincipals = new List<SqlOrphanPrincipal>();
        var (principalNamesSuccess, principalNames, _) = await _sqlInspector.GetAllPrincipalNamesAsync(integration, ct);
        if (principalNamesSuccess && principalNames is not null)
        {
            var orphanNames = principalNames
                .Where(name => !string.IsNullOrWhiteSpace(name) && !managedPrincipalNames.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (orphanNames.Count > 0)
            {
                // Use batch query for orphans too
                var (orphanOk, orphanMap, _) = await _sqlInspector.GetPrincipalPermissionsBatchAsync(
                    integration, orphanNames, ct);

                if (orphanOk && orphanMap != null)
                {
                    foreach (var orph in orphanMap.Values.Where(o => o.Exists && o.PrincipalName is not null))
                    {
                        orphanPrincipals.Add(new SqlOrphanPrincipal(
                            orph.PrincipalName!,
                            orph.Grants.Select(g => new SqlActualGrant(g.Database, g.Schema, g.Privileges)).ToList()));
                    }
                }
            }
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