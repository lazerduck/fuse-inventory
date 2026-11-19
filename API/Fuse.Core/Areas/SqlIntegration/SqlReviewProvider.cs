using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.SqlIntegration;

public sealed class SqlReviewProvider(ISqlPermissionsInspector inspector) : IIntegrationReviewProvider
{
    public string Provider => "sql";
    public IReadOnlyList<ReviewCheck> Checks { get; } = [new("account-grants", "Documented SQL Server grants", [], RequiresAccount: true)];

    public string? Validate(Snapshot s, ReviewInput input)
    {
        var integration = s.SqlIntegrations.FirstOrDefault(i => i.Id == input.IntegrationId);
        var account = s.Accounts.FirstOrDefault(a => a.Id == input.AccountId);
        return integration is not null && account is not null && account.TargetKind == TargetKind.DataStore
            && account.TargetId == integration.DataStoreId ? null : "Select an account on this SQL data store.";
    }

    public async Task<ReviewEvidence> InspectAsync(Snapshot s, ReviewInput input, CancellationToken ct)
    {
        var integration = s.SqlIntegrations.Single(i => i.Id == input.IntegrationId);
        var account = s.Accounts.Single(i => i.Id == input.AccountId);
        var result = await inspector.GetAccountStatusAsync(account, integration, s, ct);
        var status = result.Status switch { SyncStatus.InSync => ReviewStatus.Matched,
            SyncStatus.DriftDetected or SyncStatus.MissingPrincipal => ReviewStatus.Drift, _ => ReviewStatus.Unknown };
        var details = result.PermissionComparisons.SelectMany(x =>
            x.MissingPrivileges.Order().Select(p => $"{x.Database ?? "(database)"} / {x.Schema ?? "(database scope)"} · missing {p}")
            .Concat(x.ExtraPrivileges.Order().Select(p => $"{x.Database ?? "(database)"} / {x.Schema ?? "(database scope)"} · unexpected {p}"))).ToArray();
        return new(status, result.Status switch { SyncStatus.InSync => "Documented grants match SQL Server.",
            SyncStatus.MissingPrincipal => "The documented principal is missing.", SyncStatus.DriftDetected => "SQL Server and documented grants differ.",
            _ => "Grant coverage is unavailable." }, details);
    }
}
