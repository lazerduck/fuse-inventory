using System.Security.Cryptography;
using System.Text.Json;
using Fuse.Core.Areas.Audit;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Npgsql;

namespace Fuse.Core.Areas.PostgreSqlIntegration;

public sealed record GrantRepairRequest(Guid ReviewId, string? Token = null);
public sealed record GrantRepairPlan(string Database, string Schema, string Table, string Principal, string Privilege,
    bool ChangeRequired, bool Applied, string Status, IReadOnlyList<ReviewApplication> Applications, string? Token = null);
public sealed record GrantRepairState(GrantRepairPlan Plan, string Target, string State);

public sealed class PostgreSqlGrantRepairService(IFuseStore store, IAuditService audit, ICurrentUser user)
{
    public async Task<Result<GrantRepairState>> RunAsync(Guid integrationId, Guid reviewId,
        string? approvedTarget, string? approvedState, bool showApplications, CancellationToken ct)
    {
        var snapshot = await store.GetAsync(ct);
        var review = snapshot.IntegrationReviews.FirstOrDefault(r => r.Id == reviewId && r.Input.IntegrationId == integrationId &&
            r.Input.Provider == "postgresql" && r.Input.Kind == "table-access" && r.Input.Fields.GetValueOrDefault("expected") == "allow");
        if (review is null) return Failure("Select a saved required PostgreSQL table privilege.", ErrorType.NotFound);
        if (new PostgreSqlReviewProvider().Validate(snapshot, review.Input) is { } invalid) return Failure(invalid);
        var node = snapshot.PostgreSqlIntegrations.Single(n => n.Id == integrationId);
        var account = DataStoreReview.ScopedAccount(snapshot, review.Input.AccountId, node.DataStoreId)!;
        var principal = account.UserName!;
        var schema = review.Input.Fields["schema"];
        var table = review.Input.Fields["table"];
        var privilege = review.Input.Fields["privilege"];
        var apply = approvedTarget is not null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        var token = deadline.Token;
        var settings = new NpgsqlConnectionStringBuilder(PostgreSqlConnectionSettings.Build(node.Endpoint, node.Username, node.Password));
        if (apply)
        {
            settings.ApplicationName = "Fuse PostgreSQL grant repair";
            settings.Options = "-c search_path=pg_catalog -c statement_timeout=5000 -c lock_timeout=2000";
        }
        try
        {
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync(token);
            if (connection.PostgreSqlVersion.Major < 16) return Failure("Grant repair requires PostgreSQL 16 or later.");
            await using var tx = await connection.BeginTransactionAsync(token);
            // A read-only preview has no locks beyond those used by the catalog. For apply, resolve the target again
            // under a table lock. All identifier interpolation is limited to separately quoted saved identifiers.
            using var quoting = new NpgsqlCommandBuilder();
            var quotedTable = quoting.QuoteIdentifier(schema) + "." + quoting.QuoteIdentifier(table);
            if (apply)
            {
                await using var lockTable = new NpgsqlCommand($"LOCK TABLE ONLY {quotedTable} IN ACCESS EXCLUSIVE MODE NOWAIT", connection, tx);
                await lockTable.ExecuteNonQueryAsync(token);
            }
            var facts = await PostgreSqlReviewProvider.Read(connection, review.Input, principal, token, tx);
            if (facts is null) return Failure("The role or table no longer exists.", ErrorType.Conflict);
            if (!facts.InspectorOwns || !facts.IsTable) return Failure("Repair requires an ordinary or partitioned table owned by the configured PostgreSQL role.");
            if (principal is "PUBLIC" or "public" || principal.StartsWith("pg_", StringComparison.Ordinal) ||
                principal == node.Username || !facts.Login || !facts.Connect || !facts.SchemaUsage)
                return Failure("Repair requires an application login with database CONNECT and schema USAGE.");
            var applications = ReviewSources.Applications(snapshot, review);
            var plan = new GrantRepairPlan(facts.Database, schema, table, principal, privilege, !facts.Granted, false,
                facts.Granted ? "already-satisfied" : "ready", showApplications ? applications : []);
            var target = Digest(new { reviewId, review.Revision, integrationId, node.UpdatedAt, account.Id, principal, schema, table, privilege,
                facts.RoleId, facts.ObjectId, apps = applications.Select(a => a.InstanceId).Order() });
            var state = Digest(new { facts.ChangeStamp, facts.Granted, facts.Login, facts.Connect, facts.SchemaUsage, facts.Sources, facts.Assumable });
            if (!apply) return Result<GrantRepairState>.Success(new(plan, target, state));
            var latest = await store.GetAsync(token);
            if (target != approvedTarget || latest.IntegrationReviews.FirstOrDefault(r => r.Id == reviewId)?.Revision != review.Revision ||
                latest.PostgreSqlIntegrations.FirstOrDefault(i => i.Id == integrationId)?.UpdatedAt != node.UpdatedAt ||
                DataStoreReview.ScopedAccount(latest, account.Id, node.DataStoreId)?.UserName != principal ||
                !ReviewSources.Applications(latest, review).Select(a => a.InstanceId).Order().SequenceEqual(applications.Select(a => a.InstanceId).Order()))
                return Failure("The inventory target changed. Preview the repair again.", ErrorType.Conflict);
            if (facts.Granted) return Result<GrantRepairState>.Success(new(plan, target, state));
            if (state != approvedState) return Failure("PostgreSQL changed. Preview the repair again.", ErrorType.Conflict);
            await audit.LogAsync(AuditHelper.CreateLog(AuditAction.PostgreSqlGrantRepairRequested, AuditArea.PostgreSqlIntegration,
                user.UserName, user.UserId, integrationId, new { reviewId, account.Id, principal, schema, table, privilege }), token);
            // Validate enumerates permitted privileges. Identifiers and keywords are never sourced from a SQL fragment.
            await using var grant = new NpgsqlCommand($"GRANT {privilege} ON TABLE {quotedTable} TO {quoting.QuoteIdentifier(principal)}", connection, tx);
            await grant.ExecuteNonQueryAsync(token);
            var verified = await PostgreSqlReviewProvider.Read(connection, review.Input, principal, token, tx);
            if (verified is not { Usable: true } || !verified.Sources.Contains("direct grant"))
                return Failure("The direct grant did not verify. The transaction was rolled back.", ErrorType.Conflict);
            await tx.CommitAsync(token);
            await audit.LogAsync(AuditHelper.CreateLog(AuditAction.PostgreSqlGrantRepairApplied, AuditArea.PostgreSqlIntegration,
                user.UserName, user.UserId, integrationId, new { reviewId, account.Id, principal, schema, table, privilege }), CancellationToken.None);
            return Result<GrantRepairState>.Success(new(plan with { ChangeRequired = false, Applied = true, Status = "verified" }, target, state));
        }
        catch (Exception e) when (!ct.IsCancellationRequested && PostgreSqlInspector.Expected(e))
        { return Failure("Grant repair unavailable. Inspect the current state and preview again. " + PostgreSqlInspector.Status(e), ErrorType.Conflict); }
    }

    private static string Digest(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static Result<GrantRepairState> Failure(string error, ErrorType type = ErrorType.Validation) => Result<GrantRepairState>.Failure(error, type);
}
