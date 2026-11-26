using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.DataStore;
using Fuse.Core.Areas.Identity;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Areas.MessageBroker;
using Fuse.Core.Areas.ServiceBusIntegration;
using Fuse.Core.Areas.SqlIntegration;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Application;

public enum InstanceAccessStatus { Matched, Drift, Unknown, Unchecked, Undocumented }

public sealed record InstanceAccessRow(Guid DependencyId, string Provider, Guid TargetId, string TargetName,
    Guid? PrincipalId, string PrincipalName, Guid? IntegrationId, Guid? ReviewId, IReadOnlyList<string> Expected,
    IReadOnlyList<string> Observed, InstanceAccessStatus Status, DateTimeOffset? CheckedAt, bool CanConfigureReview);

public sealed record InstanceAccessResponse(IReadOnlyList<InstanceAccessRow> Rows);

public static class InstanceAccess
{
    public static InstanceAccessResponse Describe(Snapshot snapshot, ApplicationInstance instance, Func<string, bool> can)
    {
        var rows = new List<InstanceAccessRow>();
        var readSql = can(AccountPermissions.ReadKey) && can(DataStorePermissions.ReadKey) && can(SqlIntegrationPermissions.ReadKey);
        var readBus = can(IdentityPermissions.ReadKey) && can(MessageBrokerPermissions.ReadKey) && can(ServiceBusIntegrationPermissions.ReadKey);
        var readReviews = can(IntegrationReviewPermissions.ReadKey);
        var manageReviews = readReviews && can(IntegrationReviewPermissions.ManageKey);
        var reviews = snapshot.IntegrationReviews.Where(r => readReviews &&
            (r.ApplicationInstanceIds.Length == 0 || r.ApplicationInstanceIds.Contains(instance.Id))).ToLookup(r => r.Input.IntegrationId);

        foreach (var dependency in instance.Dependencies)
        {
            if (readSql && dependency is { AuthKind: DependencyAuthKind.Account, TargetKind: TargetKind.DataStore, AccountId: {} accountId } &&
                snapshot.DataStores.FirstOrDefault(t => t.Id == dependency.TargetId) is {} store)
            {
                var sources = snapshot.SqlIntegrations.Where(i => i.DataStoreId == store.Id).Select(i => i.Id).ToArray();
                if (sources.Length == 0 && store.Kind.Trim().ToLowerInvariant() is not ("sql" or "sql server" or "sqlserver" or "mssql")) continue;
                var account = snapshot.Accounts.FirstOrDefault(a => a.Id == accountId && a.TargetKind == TargetKind.DataStore && a.TargetId == store.Id);
                var row = new InstanceAccessRow(dependency.Id, "sql", store.Id, store.Name, account?.Id, account?.UserName ?? "Unknown account",
                    sources.Length == 1 ? sources[0] : null, null, [], [], InstanceAccessStatus.Unchecked, null,
                    manageReviews && sources.Length > 0 && !string.IsNullOrWhiteSpace(account?.UserName));
                if (account is null)
                {
                    rows.Add(row with { Status = InstanceAccessStatus.Unknown, Observed = ["The dependency account is unavailable on this data store."] });
                    continue;
                }
                var expected = account.Grants.Where(g => SqlPermissionDiff.IsDatabaseScope(g.Database, g.Schema))
                    .GroupBy(g => g.Database!, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => (Database: g.Key, Grants: g.SelectMany(v => v.Privileges).Distinct().Order().ToArray()))
                    .Where(g => g.Grants.Length != 0)
                    .Select(g => $"{g.Database} · {string.Join(", ", g.Grants)} (whole database)").ToArray();
                var checks = sources.SelectMany(id => reviews[id]).Where(r => r.Input is { Provider: "sql", Kind: "account-grants" } &&
                    r.Input.AccountId == accountId);
                rows.AddRange(Project(snapshot, row with { Expected = expected }, checks, readReviews));
            }
            else if (readBus && dependency is { AuthKind: DependencyAuthKind.Identity, TargetKind: TargetKind.MessageBroker, IdentityId: {} identityId } &&
                snapshot.MessageBrokers.FirstOrDefault(t => t.Id == dependency.TargetId) is {} broker &&
                broker.Kind.Equals("Azure Service Bus", StringComparison.OrdinalIgnoreCase))
            {
                var identity = snapshot.Identities.FirstOrDefault(i => i.Id == identityId && (i.OwnerInstanceId == null || i.OwnerInstanceId == instance.Id));
                if (identity is not { Kind: IdentityKind.AzureManagedIdentity }) continue;
                var sources = snapshot.ServiceBusIntegrations.Where(i => i.MessageBrokerId == broker.Id).Select(i => i.Id).ToArray();
                var roles = identity.Assignments.Where(a => a.TargetKind == TargetKind.MessageBroker && a.TargetId == broker.Id && !string.IsNullOrWhiteSpace(a.Role))
                    .Select(a => a.Role!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                var row = new InstanceAccessRow(dependency.Id, "servicebus", broker.Id, broker.Name, identity.Id, identity.Name,
                    sources.Length == 1 ? sources[0] : null, null, roles, [], InstanceAccessStatus.Unchecked, null,
                    manageReviews && sources.Length > 0 && identity.AzurePrincipal != null);
                var checks = sources.SelectMany(id => reviews[id]).Where(r => r.Input is { Provider: "servicebus", Kind: "data-role", AccountId: null } &&
                    Guid.TryParse(r.Input.Fields.GetValueOrDefault("identityId"), out var id) && id == identityId);
                rows.AddRange(Project(snapshot, row, checks, readReviews));
            }
        }
        return new(rows);
    }

    private static IEnumerable<InstanceAccessRow> Project(Snapshot snapshot, InstanceAccessRow dependency,
        IEnumerable<ReviewDefinition> reviews, bool readReviews)
    {
        if (!readReviews)
        {
            yield return dependency with { Status = InstanceAccessStatus.Unknown, Observed = ["Review evidence is unavailable with your permissions."] };
            yield break;
        }
        var any = false;
        foreach (var review in reviews.OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id))
        {
            any = true;
            var row = dependency with { IntegrationId = review.Input.IntegrationId, ReviewId = review.Id };
            if (row.Provider == "servicebus")
            {
                var fields = review.Input.Fields;
                var scope = fields.GetValueOrDefault("assignmentScope") == "Queue only" ? "queue scope only" : "queue or inherited scope";
                row = row with { Expected = [$"{fields.GetValueOrDefault("role")} on queue {fields.GetValueOrDefault("queue")} ({scope})"] };
            }
            if (review.ScopeFingerprint != ReviewSources.Scope(snapshot, review.Input))
            {
                yield return row with { Status = InstanceAccessStatus.Unknown, Observed = ["The connection or principal changed. Review and save the expectation again."] };
                continue;
            }
            if (review.LastAttempt is not {} attempt)
            {
                yield return Unchecked(row);
                continue;
            }
            var evidence = attempt.Evidence;
            if (row.Expected.Count == 0)
            {
                // An empty grant list can mean the account has never been documented.
                // The existing SQL drift report compares against an exact empty set.
                yield return row with { Status = InstanceAccessStatus.Undocumented, CheckedAt = attempt.CheckedAt,
                    Observed = evidence.ObservedAccess ?? (evidence.Status == ReviewStatus.Unknown ? [evidence.Summary] : []) };
                continue;
            }
            yield return row with { CheckedAt = attempt.CheckedAt, Status = evidence.Status switch
            {
                ReviewStatus.Matched => InstanceAccessStatus.Matched,
                ReviewStatus.Drift => InstanceAccessStatus.Drift,
                _ => InstanceAccessStatus.Unknown
            }, Observed = EvidenceLines(evidence) };
        }
        if (!any)
        {
            var row = Unchecked(dependency);
            if (row.Provider == "servicebus" && row.Expected.Count != 0)
                row = row with { Observed = ["A queue and built-in role expectation is needed to check this documented assignment."] };
            yield return row;
        }
    }

    private static IReadOnlyList<string> EvidenceLines(ReviewEvidence evidence)
    {
        if (evidence.ObservedAccess is not { Count: > 0 } facts)
            return [evidence.Summary, ..evidence.Details];
        // Lead with the discrepancy and the scoped observation side by side in the card preview.
        return [..evidence.Details.Take(1), ..facts.Take(1), ..evidence.Details.Skip(1), ..facts.Skip(1)];
    }

    private static InstanceAccessRow Unchecked(InstanceAccessRow row) =>
        row.Expected.Count == 0 ? row with { Status = InstanceAccessStatus.Undocumented } : row;
}
