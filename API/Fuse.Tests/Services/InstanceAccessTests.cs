using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Areas.SqlIntegration;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public sealed class InstanceAccessTests
{
    private readonly DataStore database = new(Guid.NewGuid(), "Orders SQL", null, "SQL Server", Guid.NewGuid(), null, null, [], default, default);
    private readonly MessageBroker broker = new(Guid.NewGuid(), "Messages", null, "Azure Service Bus", Guid.NewGuid(), null, [], default, default);
    private readonly Grant select = new(Guid.NewGuid(), "Orders", null, [Privilege.Select]);
    private readonly Grant legacy = new(Guid.NewGuid(), "Archive", "dbo", [Privilege.Control]);
    private readonly Account account;
    private readonly Identity identity;
    private readonly SqlIntegration sql;
    private readonly ServiceBusConnection bus;
    private readonly ApplicationInstance instance;
    private readonly Snapshot snapshot;
    private readonly ReviewInput sqlCheck;
    private readonly ReviewInput busCheck;

    public InstanceAccessTests()
    {
        account = new(Guid.NewGuid(), database.Id, TargetKind.DataStore, AuthKind.UserPassword,
            new(SecretBindingKind.PlainReference, "sensitive-account-reference", null), "orders_reader", null, [select, legacy], [], default, default);
        identity = new(Guid.NewGuid(), "Orders worker", IdentityKind.AzureManagedIdentity, null, null,
            [new(Guid.NewGuid(), TargetKind.MessageBroker, broker.Id, "Event publisher", null)], [], default, default, new(Guid.NewGuid(), Guid.NewGuid()));
        sql = new(Guid.NewGuid(), "Integration", database.Id, "Server=sql;Database=master;Password=sensitive-password", null, SqlPermissions.Read, default, default);
        bus = new(Guid.NewGuid(), "Bus", broker.Id, "/subscriptions/sub/resourceGroups/lab/providers/Microsoft.ServiceBus/namespaces/lab",
            SecretProviderAuthMode.ManagedIdentity, default, default);
        instance = new(Guid.NewGuid(), database.EnvironmentId, null, null, null, null, null,
            [new(Guid.NewGuid(), database.Id, TargetKind.DataStore, null, DependencyAuthKind.Account, account.Id, null),
             new(Guid.NewGuid(), broker.Id, TargetKind.MessageBroker, null, DependencyAuthKind.Identity, null, identity.Id)], [], default, default);
        snapshot = new InMemoryFuseStore().Current! with { DataStores = [database], MessageBrokers = [broker], Accounts = [account],
            Identities = [identity], SqlIntegrations = [sql], ServiceBusIntegrations = [bus] };
        sqlCheck = new("sql", sql.Id, "account-grants", account.Id, new());
        busCheck = new("servicebus", bus.Id, "data-role", null,
            new() { ["queue"] = "orders", ["role"] = "Sender", ["identityId"] = identity.Id.ToString(), ["assignmentScope"] = "Queue only" });
    }

    private static ReviewDefinition Saved(Snapshot s, ReviewInput input, ReviewAttempt? attempt, params Guid[] instances) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Review", input, 0, instances, attempt, attempt, [], ReviewSources.Scope(s, input));

    private IReadOnlyList<InstanceAccessRow> Rows(Snapshot s, string? denied = null) =>
        InstanceAccess.Describe(s, instance, permission => permission != denied).Rows;

    [Fact]
    public void OnlyExactDependencyAndTargetMatchesContributeEvidence()
    {
        var time = DateTimeOffset.UtcNow.AddDays(-1);
        var checkedSql = Saved(snapshot, sqlCheck, new(time, new(ReviewStatus.Matched, "Verified SQL", [])));
        var checkedBus = Saved(snapshot, busCheck, new(time.AddHours(1), new(ReviewStatus.Drift, "Namespace assignment", [])), instance.Id);
        var wrongTarget = bus with { Id = Guid.NewGuid(), MessageBrokerId = Guid.NewGuid() };
        var s = snapshot with { ServiceBusIntegrations = [bus, wrongTarget], IntegrationReviews = [
            checkedSql, checkedBus, checkedSql with { ApplicationInstanceIds = [Guid.NewGuid()] },
            checkedSql with { Input = sqlCheck with { AccountId = Guid.NewGuid() } },
            checkedBus with { Input = busCheck with { IntegrationId = wrongTarget.Id } },
            checkedBus with { Input = busCheck with { Fields = new(busCheck.Fields) { ["identityId"] = Guid.NewGuid().ToString() } } }] };
        var rows = Rows(s);
        Assert.Collection(rows, row =>
        {
            Assert.Equal(checkedSql.Id, row.ReviewId);
            Assert.Equal(InstanceAccessStatus.Matched, row.Status);
            Assert.Equal(time, row.CheckedAt);
            Assert.Contains("Orders · Select (whole database)", row.Expected);
            Assert.DoesNotContain(row.Expected, s => s.Contains("Archive"));
        }, row =>
        {
            Assert.Equal(checkedBus.Id, row.ReviewId);
            Assert.Equal(InstanceAccessStatus.Drift, row.Status);
            Assert.Equal(time.AddHours(1), row.CheckedAt);
            Assert.Contains("Sender on queue orders (queue scope only)", row.Expected);
            Assert.Contains("Namespace assignment", row.Observed);
        });
        Assert.DoesNotContain("sensitive", System.Text.Json.JsonSerializer.Serialize(rows));
    }

    [Theory]
    [InlineData("accounts:read", "servicebus")]
    [InlineData("sqlintegrations:read", "servicebus")]
    [InlineData("datastores:read", "servicebus")]
    [InlineData("identities:read", "sql")]
    [InlineData("servicebusintegrations:read", "sql")]
    [InlineData("messagebrokers:read", "sql")]
    public void RequiresTargetPrincipalAndIntegrationVisibility(string denied, string visible) =>
        Assert.Equal(visible, Assert.Single(Rows(snapshot, denied)).Provider);

    [Fact]
    public void OldOrHiddenEvidenceCannotBePresentedAsCurrent()
    {
        var time = DateTimeOffset.UtcNow;
        var ok = new ReviewAttempt(time, new(ReviewStatus.Matched, "Old verified result", []));
        var s = snapshot with {
            IntegrationReviews = [Saved(snapshot, sqlCheck, ok), Saved(snapshot, busCheck, ok) with {
                LastAttempt = new(time.AddHours(1), new(ReviewStatus.Unknown, "Limited ARM visibility", [])) }],
            Accounts = [account with { Grants = [select with { Privileges = [Privilege.Insert] }] }] };
        var rows = Rows(s);
        Assert.All(rows, r => Assert.Equal(InstanceAccessStatus.Unknown, r.Status));
        Assert.Null(rows[0].CheckedAt);
        Assert.Contains("Limited ARM visibility", rows[1].Observed);
        Assert.Equal(time.AddHours(1), rows[1].CheckedAt);
        Assert.DoesNotContain("Old verified result", System.Text.Json.JsonSerializer.Serialize(rows));
        foreach (var hidden in Rows(s, "integrationreviews:read"))
        {
            Assert.Null(hidden.ReviewId);
            Assert.Null(hidden.CheckedAt);
            Assert.DoesNotContain("Limited ARM visibility", hidden.Observed);
        }
    }

    [Fact]
    public void LegacyOrAbsentExpectationsCannotConvertObservedAccessIntoDrift()
    {
        var s = snapshot with { Accounts = [account with { Grants = [legacy] }] };
        var attempt = new ReviewAttempt(DateTimeOffset.UtcNow, new(ReviewStatus.Drift, "Different from an empty set", ["Unexpected Select"])
            { ObservedAccess = ["Orders · Select (whole database)"] });
        s = s with { IntegrationReviews = [Saved(s, sqlCheck, attempt)] };
        var rows = Rows(s);
        Assert.Equal(InstanceAccessStatus.Undocumented, rows[0].Status);
        Assert.Empty(rows[0].Expected);
        Assert.Equal(new[] { "Orders · Select (whole database)" }, rows[0].Observed);
        Assert.Equal(InstanceAccessStatus.Unchecked, rows[1].Status);
        Assert.Equal(new[] { "Event publisher" }, rows[1].Expected);
        Assert.Contains("A queue and built-in role expectation is needed", Assert.Single(rows[1].Observed));
    }

    [Fact]
    public async Task SqlEvidenceRecordsScopedAndInheritedAccessIndependentlyOfDatabaseDrift()
    {
        var inspector = new Mock<ISqlPermissionsInspector>();
        var roles = new[] { new SqlPermissionEvidence("Object", "dbo", "p", null, "GRANT", "readers", null, true, "EXECUTE") };
        var s = snapshot with { Accounts = [account with { Grants = [select with { Privileges = [Privilege.Select, Privilege.Execute] }] }] };
        inspector.Setup(i => i.GetAccountStatusAsync(s.Accounts[0], sql, s, default)).ReturnsAsync(
            new AccountSqlStatusResponse(account.Id, sql.Id, sql.Name, SyncStatus.DriftDetected, "Missing database execute",
                [new("Orders", null, [Privilege.Select, Privilege.Execute], [Privilege.Select], [Privilege.Execute], [])], null)
            {
                Coverage = [new SqlPrivilegeCoverage("Orders", "EXECUTE", SqlCoverageLevel.Some, roles, "CurrentObjects") { ProcedureAccess = new(30, 50) },
                    new("Archive", "EXECUTE", SqlCoverageLevel.Unknown, [], "Incomplete")]
            });
        var provider = new SqlReviewProvider(inspector.Object, Mock.Of<IAccountSqlInspector>());
        var result = await provider.InspectAsync(s, sqlCheck, default);
        Assert.Equal(ReviewStatus.Drift, result.Status);
        Assert.Contains("Orders · Select (whole database)", result.ObservedAccess!);
        Assert.Contains("Orders · EXECUTE: Some. Can execute 30 of 50 procedures (includes grants through readers)", result.ObservedAccess!);
        Assert.Contains("Archive · EXECUTE: Unknown", result.ObservedAccess!);
        var row = Rows(s with { IntegrationReviews = [Saved(s, sqlCheck, new(DateTimeOffset.UtcNow, result))] })[0];
        Assert.Equal("Orders · missing whole-database Execute", row.Observed[0]);
        Assert.Contains("Can execute 30 of 50 procedures", row.Observed[1]);
    }
}
