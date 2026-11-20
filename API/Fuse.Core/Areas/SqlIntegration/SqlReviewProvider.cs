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
            x.MissingPrivileges.Order().Select(p => $"{x.Database} · missing {p}")
            .Concat(x.ExtraPrivileges.Order().Select(p => $"{x.Database} · unexpected {p}"))).ToArray();
        return new(status, result.Status switch { SyncStatus.InSync => "Whole-database grants match the configuration.",
            SyncStatus.MissingPrincipal => "The documented principal is missing.", SyncStatus.DriftDetected => "Whole-database grants differ from the configuration.",
            _ => result.CoverageWarning ?? "Grant coverage is unavailable." }, details);
    }
}
