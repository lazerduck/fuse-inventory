using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Models;
using Fuse.Core.Areas.Account;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.SqlIntegration;

public sealed class SqlReviewProvider(ISqlPermissionsInspector inspector, IAccountSqlInspector catalog) : IIntegrationReviewProvider
{
    public string Provider => "sql";
    public IReadOnlyList<ReviewCheck> Checks { get; } = [new("account-grants", "Documented whole-database grants", [], RequiresAccount: true)];

    public string? Validate(Snapshot s, ReviewInput input)
    {
        var integration = s.SqlIntegrations.FirstOrDefault(i => i.Id == input.IntegrationId);
        var account = s.Accounts.FirstOrDefault(a => a.Id == input.AccountId);
        return input.Kind == "account-grants" && input.Fields.Count == 0 && integration is not null && account is not null &&
            !string.IsNullOrWhiteSpace(account.UserName) && account.TargetKind == TargetKind.DataStore
            && account.TargetId == integration.DataStoreId ? null : "Select an account on this SQL data store.";
    }

    public async Task<ReviewEvidence> InspectAsync(Snapshot s, ReviewInput input, CancellationToken ct)
    {
        var integration = s.SqlIntegrations.Single(i => i.Id == input.IntegrationId);
        var account = s.Accounts.Single(i => i.Id == input.AccountId);
        if (!integration.Permissions.HasFlag(SqlPermissions.Read))
            return new(ReviewStatus.Unknown, "SQL Server read permission is unavailable.", []);
        if (await catalog.InspectGrantCoverageAsync(integration, ct) is {} incomplete)
            return new(ReviewStatus.Unknown, incomplete, []);
        var result = await inspector.GetAccountStatusAsync(account, integration, s, ct);
        var status = result.Status switch { SyncStatus.InSync => ReviewStatus.Matched,
            SyncStatus.DriftDetected or SyncStatus.MissingPrincipal => ReviewStatus.Drift, _ => ReviewStatus.Unknown };
        var details = result.PermissionComparisons.SelectMany(x =>
            x.MissingPrivileges.Order().Select(p => $"{x.Database} · missing whole-database {p}")
            .Concat(x.ExtraPrivileges.Order().Select(p => $"{x.Database} · unexpected whole-database {p}"))).ToArray();
        return new(status, result.Status switch { SyncStatus.InSync => "Whole-database grants match the configuration.",
            SyncStatus.MissingPrincipal => "The documented principal is missing.", SyncStatus.DriftDetected => "Whole-database grants differ from the configuration.",
            _ => result.CoverageWarning ?? "Grant coverage is unavailable." }, details)
        {
            ObservedAccess = DescribeAccess(result)
        };
    }

    private static IReadOnlyList<string> DescribeAccess(AccountSqlStatusResponse result)
    {
        if (result.Status == SyncStatus.MissingPrincipal) return ["SQL Server did not find the principal."];
        if (result.Status is not (SyncStatus.InSync or SyncStatus.DriftDetected))
            return [result.CoverageWarning ?? "SQL Server could not verify this account’s grants."];

        var wholeDatabase = result.PermissionComparisons.Where(c => c.ActualPrivileges.Count != 0)
            .OrderBy(c => c.Database, StringComparer.Ordinal)
            .Select(c => $"{c.Database} · {string.Join(", ", c.ActualPrivileges.Order())} (whole database)");
        var scopes = result.Coverage.Where(c => Enum.TryParse<Privilege>(c.Permission, true, out _) &&
            (c.Basis != "DatabaseGrant" || c.ProcedureAccess != null || c.Evidence.Any(e => e.Inherited)))
            .OrderByDescending(c => c.Level == SqlCoverageLevel.Some && c.ProcedureAccess != null)
            .ThenByDescending(c => c.Level == SqlCoverageLevel.Some)
            .ThenBy(c => c.Database, StringComparer.Ordinal).ThenBy(c => c.Permission, StringComparer.Ordinal)
            .Select(c =>
            {
                var level = c.Basis == "CurrentObjects" && c.Level == SqlCoverageLevel.All ? "All current objects" : c.Level.ToString();
                var line = $"{c.Database} · {c.Permission}: {level}";
                if (c.ProcedureAccess is {} count)
                    line += $". Can execute {count.Executable} of {count.Total} {(count.Total == 1 ? "procedure" : "procedures")}";
                var inherited = c.Evidence.Where(e => e.Inherited && e.State is "GRANT" or "GRANT_WITH_GRANT_OPTION")
                    .Select(e => e.Grantee).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (inherited.Length != 0) line += $" (includes grants through {string.Join(", ", inherited)})";
                return line;
            });
        var facts = scopes.Concat(wholeDatabase).ToArray();
        if (facts.Length == 0) return ["No managed database privileges were observed."];
        const int limit = 100;
        return facts.Length > limit ? [.. facts.Take(limit), $"{facts.Length - limit} more entries in the SQL permissions overview."] : facts;
    }
}
