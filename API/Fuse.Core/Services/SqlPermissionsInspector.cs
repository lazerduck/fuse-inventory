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

            var (isSuccessful, actualPermissions, errorMessage) = await _sqlInspector.GetPrincipalPermissionsAsync(
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

        var orphanPrincipals = new List<SqlOrphanPrincipal>();
        var (allPrincipalsSuccess, allPrincipals, _) = await _sqlInspector.GetAllPrincipalsAsync(integration, ct);
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
