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

        var status = await GetPermissionsOverviewAsync(integrationId, ct);
        if (status.Value is not {} overview) return Result<SqlBulkPlan>.Failure("Permissions could not be inspected.", status);
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
                operations.Add(new(account.Id, user, "Skip", Reason: "Incomplete privilege or principal coverage."));
                continue;
            }
            if (account.Grants.Any(g => string.IsNullOrWhiteSpace(g.Database)))
            {
                operations.Add(new(account.Id, user, "Skip", Reason: "Choose an explicit database for every grant."));
                continue;
            }
            if (row.Status == SyncStatus.DriftDetected)
            {
                if (InheritedExtra(account.Id, user, row.PermissionComparisons) is {} inheritedSkip) operations.Add(inheritedSkip);
                else operations.AddRange(PrivilegeOperations(account.Id, user, row.PermissionComparisons));
                continue;
            }
            if (!HasSecretProviderBinding(account))
            {
                operations.Add(new(account.Id, user, "Skip", Reason: "No linked secret provider."));
                continue;
            }
            var databases = account.Grants.Select(g => g.Database!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var baseline = databases.Length == 0 ? new SqlPrincipalPermissions(user, true, [])
                : await _sqlInspector.GetNewUserBaselineAsync(integration, databases, ct);
            if (baseline is null)
                return Result<SqlBulkPlan>.Failure("The new user's database privileges cannot be verified. Review the account separately.", ErrorType.Conflict);
            var comparisons = SqlPermissionDiff.BuildComparisons(account.Grants, baseline);
            if (InheritedExtra(account.Id, user, comparisons) is {} fromPublic)
            {
                operations.Add(fromPublic);
                continue;
            }
            operations.Add(new(account.Id, user, "Create login"));
            operations.AddRange(databases.Select(d => new SqlBulkOperation(account.Id, user, "Create user", d)));
            operations.AddRange(PrivilegeOperations(account.Id, user, comparisons));
        }
        var fingerprint = new { integration.Id, integration.DataStoreId, integration.UpdatedAt, operations,
            accounts = accounts.Values.OrderBy(a => a.Id).Select(a => new { a.Id, a.UpdatedAt }) };
        var revision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(fingerprint)));
        return Result<SqlBulkPlan>.Success(new(revision, operations));
    }

    private static SqlBulkOperation? InheritedExtra(Guid id, string user, IReadOnlyList<SqlPermissionComparison> comparisons)
    {
        foreach (var comparison in comparisons.OrderBy(g => g.Database, StringComparer.Ordinal).ThenBy(g => g.Schema, StringComparer.Ordinal))
            foreach (var privilege in comparison.ExtraPrivileges.Intersect(comparison.Inherited).Order())
                return new(id, user, "Skip", comparison.Database, comparison.Schema, privilege,
                    "An inherited privilege requires review of its role or PUBLIC grant.");
        return null;
    }

    private static IEnumerable<SqlBulkOperation> PrivilegeOperations(Guid id, string user, IReadOnlyList<SqlPermissionComparison> comparisons) =>
        comparisons.OrderBy(g => g.Database, StringComparer.Ordinal).ThenBy(g => g.Schema, StringComparer.Ordinal)
            .SelectMany(g => g.MissingPrivileges.Order().Select(p => new SqlBulkOperation(id, user, "Grant", g.Database, g.Schema, p))
                .Concat(g.ExtraPrivileges.Order().Select(p => new SqlBulkOperation(id, user, "Revoke", g.Database, g.Schema, p))));
}
