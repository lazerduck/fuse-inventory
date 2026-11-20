using System.Security.Cryptography;
using System.Text.Json;
using Fuse.Core.Areas.Account;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.SqlIntegration;

public sealed record SqlBulkOperation(Guid AccountId, string Principal, string Operation, string? Database = null,
    string? Schema = null, Privilege? Privilege = null, string? Reason = null);
public sealed record SqlBulkPlan(string Revision, IReadOnlyList<SqlBulkOperation> Operations);

public partial class SqlIntegrationService
{
    public async Task<Result<SqlBulkPlan>> PreviewBulkAsync(Guid integrationId, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);
        var integration = snapshot.SqlIntegrations.FirstOrDefault(i => i.Id == integrationId);
        if (integration is null) return Result<SqlBulkPlan>.Failure("SQL integration not found.", ErrorType.NotFound);
        if (!integration.Permissions.HasFlag(SqlPermissions.Read | SqlPermissions.Write | SqlPermissions.Create))
            return Result<SqlBulkPlan>.Failure("Bulk resolution requires a SQL reader with write and create permissions.");
        if (await _sqlInspector.InspectGrantCoverageAsync(integration, ct) is { } incomplete)
            return Result<SqlBulkPlan>.Failure(incomplete, ErrorType.Conflict);

        var overview = await _permissions.GetOverviewAsync(integration, snapshot, includeOrphans: false, ct);
        return await BuildBulkPlan(integration, snapshot, overview, ct);
    }

    private async Task<Result<SqlBulkPlan>> BuildBulkPlan(Models.SqlIntegration integration, Snapshot snapshot,
        SqlIntegrationPermissionsOverviewResponse overview, CancellationToken ct)
    {
        var operations = new List<SqlBulkOperation>();
        var accounts = snapshot.Accounts.Where(a => a.TargetKind == TargetKind.DataStore && a.TargetId == integration.DataStoreId)
            .ToDictionary(a => a.Id);
        foreach (var row in overview.Accounts.OrderBy(a => a.AccountId))
        {
            if (!accounts.TryGetValue(row.AccountId, out var account))
                return Result<SqlBulkPlan>.Failure("The inventory changed. Preview again.", ErrorType.Conflict);
            var user = row.PrincipalName ?? "";
            if (row.Status == SyncStatus.InSync) continue;
            if (row.Status is not (SyncStatus.DriftDetected or SyncStatus.MissingPrincipal))
            {
                operations.Add(new(account.Id, user, "Skip", Reason: row.CoverageWarning ?? row.ErrorMessage ?? "Incomplete privilege or principal coverage."));
                continue;
            }
            if (row.Status == SyncStatus.DriftDetected)
            {
                operations.AddRange(PrivilegeOperations(account.Id, user, row.PermissionComparisons));
                continue;
            }
            var databases = account.Grants.Where(g => SqlPermissionDiff.IsDatabaseScope(g.Database, g.Schema))
                .Select(g => g.Database!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (account.Grants.Count != 0 && databases.Length == 0)
            {
                operations.Add(new(account.Id, user, "Skip", Reason: "This account has only legacy declarations outside whole-database scope."));
                continue;
            }
            if (!HasSecretProviderBinding(account))
            {
                operations.Add(new(account.Id, user, "Skip", Reason: "No linked secret provider."));
                continue;
            }
            var baseline = databases.Length == 0 ? new SqlPrincipalPermissions(user, true, [])
                : await _sqlInspector.GetNewUserBaselineAsync(integration, databases, ct);
            if (baseline is null || baseline.CoverageWarning is not null || baseline.InspectionError is not null)
                return Result<SqlBulkPlan>.Failure("The new user's database privileges cannot be verified. Review the account separately.", ErrorType.Conflict);
            baseline = SqlPermissionDiff.CheckDatabaseNames(account.Grants, baseline);
            if (baseline.CoverageWarning is { } warning)
            {
                operations.Add(new(account.Id, user, "Skip", Reason: warning));
                continue;
            }
            var comparisons = SqlPermissionDiff.BuildComparisons(account.Grants, baseline);
            var privileges = PrivilegeOperations(account.Id, user, comparisons).ToArray();
            if (privileges.FirstOrDefault(o => o.Operation == "Skip") is {} fromPublic)
            {
                operations.Add(fromPublic);
                continue;
            }
            operations.Add(new(account.Id, user, "Create login"));
            operations.AddRange(databases.Select(d => new SqlBulkOperation(account.Id, user, "Create user", d)));
            operations.AddRange(privileges);
        }
        var fingerprint = new { integration.Id, integration.DataStoreId, integration.UpdatedAt, operations,
            accounts = accounts.Values.OrderBy(a => a.Id).Select(a => new { a.Id, a.UpdatedAt }) };
        var revision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(fingerprint)));
        return Result<SqlBulkPlan>.Success(new(revision, operations));
    }

    private static IEnumerable<SqlBulkOperation> PrivilegeOperations(Guid id, string user, IReadOnlyList<SqlPermissionComparison> comparisons)
    {
        foreach (var comparison in comparisons.OrderBy(g => g.Database, StringComparer.Ordinal))
        {
            foreach (var privilege in comparison.MissingPrivileges.Concat(comparison.ExtraPrivileges).Order())
            {
                if (comparison.BlockedPrivileges.Contains(privilege))
                    yield return new(id, user, "Skip", comparison.Database, null, privilege,
                        $"Review {privilege} in {comparison.Database}: an inherited permission, DENY or grant option requires manual review.");
                else
                    yield return new(id, user, comparison.MissingPrivileges.Contains(privilege) ? "Grant" : "Revoke",
                        comparison.Database, null, privilege);
            }
        }
    }
}
