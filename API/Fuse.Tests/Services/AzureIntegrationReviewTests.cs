using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Fuse.Core.Areas.AzureMetadata;
using Fuse.Core.Areas.CosmosDbIntegration;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Areas.ServiceBusIntegration;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public sealed class AzureIntegrationReviewTests
{
    private const string Prefix = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/lab/providers/";
    private static readonly ServiceBusConnection Bus = new(Guid.NewGuid(), "Bus", Guid.NewGuid(), Prefix + "Microsoft.ServiceBus/namespaces/lab-bus",
        SecretProviderAuthMode.ManagedIdentity, DateTime.UtcNow, DateTime.UtcNow);
    private static readonly CosmosDbConnection Cosmos = new(Guid.NewGuid(), "Cosmos", Guid.NewGuid(), Prefix + "Microsoft.DocumentDB/databaseAccounts/lab-cosmos",
        SecretProviderAuthMode.ManagedIdentity, DateTime.UtcNow, DateTime.UtcNow);
    private static readonly AzurePrincipalBinding Binding = new(Guid.NewGuid(), Guid.NewGuid());
    private static readonly Identity Identity = new(Guid.NewGuid(), "Workload", IdentityKind.Custom, null, null, [], [], default, default, Binding);
    private static readonly Snapshot State = new InMemoryFuseStore().Current! with
        { ServiceBusIntegrations = [Bus], CosmosDbIntegrations = [Cosmos], Identities = [Identity] };
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private static string Jwt => "header." + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { tid = Binding.TenantId }))).TrimEnd('=') + ".signature";
    private static ITokenCredentialFactory Factory()
    {
        var f = new Mock<ITokenCredentialFactory>();
        f.Setup(x => x.Create(It.IsAny<SecretProviderAuthMode>(), It.IsAny<SecretProviderCredentials?>())).Returns(new Credential(Jwt));
        return f.Object;
    }
    private static HttpClient Http(Func<Uri, HttpResponseMessage> get) => new(new Reads(get));
    private sealed class Reads(Func<Uri, HttpResponseMessage> get) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, message.Method);
            Assert.Equal(Jwt, message.Headers.Authorization?.Parameter);
            return Task.FromResult(get(message.RequestUri!));
        }
    }
    private sealed class Credential(string token) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext request, CancellationToken ct) => new(token, DateTimeOffset.UtcNow.AddHours(1));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext request, CancellationToken ct) => ValueTask.FromResult(GetToken(request, ct));
    }

    [Theory]
    [InlineData(false, "Matched")]
    [InlineData(true, "Unknown")]
    public async Task BusQueueRoleDistinguishesConditionalAndUnconditionalAssignments(bool conditional, string expected)
    {
        var scope = Bus.NamespaceResourceId + "/queues/orders";
        using var http = Http(url =>
        {
            Assert.Equal("management.azure.com", url.Host);
            Assert.Equal(scope + "/providers/Microsoft.Authorization/roleAssignments", url.AbsolutePath);
            Assert.Contains("atScope", url.Query);
            return Json(new { value = new[] { new { properties = new { principalId = Binding.ObjectId, scope = Bus.NamespaceResourceId,
                roleDefinitionId = "/providers/Microsoft.Authorization/roleDefinitions/69a216fc-b8fb-44d8-bc22-1f3c2cd27a39", condition = conditional ? "condition" : null } } } });
        });
        var provider = new ServiceBusReviewProvider(new(http, Mock.Of<IAzureIntegrationManagerService>(), Factory()));
        var input = new ReviewInput("servicebus", Bus.Id, "data-role", null, new() { ["identityId"] = Identity.Id.ToString(), ["queue"] = "orders", ["role"] = "Sender" });
        Assert.Null(provider.Validate(State, input));
        Assert.Equal(expected, (await provider.InspectAsync(State, input, default)).Status.ToString());
    }

    [Theory]
    [InlineData(null, true, false, ReviewStatus.Matched)]
    [InlineData("Queue only", true, false, ReviewStatus.Drift)]
    [InlineData("Queue only", true, true, ReviewStatus.Unknown)]
    [InlineData("Queue only", false, false, ReviewStatus.Matched)]
    public async Task BusReviewChecksTheScopeOfAllMatchingDirectGrants(string? limit, bool inherited, bool conditional, ReviewStatus expected)
    {
        var queue = Bus.NamespaceResourceId + "/queues/orders";
        object Grant(string scope, bool hasCondition) => new { properties = new { principalId = Binding.ObjectId, scope,
            roleDefinitionId = "/providers/Microsoft.Authorization/roleDefinitions/69a216fc-b8fb-44d8-bc22-1f3c2cd27a39",
            condition = hasCondition ? "condition" : null } };
        List<object> grants = [Grant(queue, false), Grant(Bus.NamespaceResourceId + "/queues/elsewhere", false)];
        if (inherited) grants.Add(Grant(Bus.NamespaceResourceId, conditional));
        using var http = Http(_ => Json(new { value = grants }));
        var provider = new ServiceBusReviewProvider(new(http, Mock.Of<IAzureIntegrationManagerService>(), Factory()));
        var input = new ReviewInput("servicebus", Bus.Id, "data-role", null, new() { ["identityId"] = Identity.Id.ToString(), ["queue"] = "orders", ["role"] = "Sender" });
        if (limit is not null) input.Fields["assignmentScope"] = limit;
        var evidence = await provider.InspectAsync(State, input, default);
        Assert.Equal(expected, evidence.Status);
        Assert.Contains("Scope: " + queue, evidence.Details);
        Assert.DoesNotContain(evidence.Details, line => line.Contains("elsewhere"));
        Assert.Contains(inherited && !conditional ? "namespace lab-bus" : "queue orders", evidence.Summary);
    }

    [Theory]
    [InlineData(false, ReviewStatus.Matched)]
    [InlineData(true, ReviewStatus.Unknown)]
    public async Task BusQueueGrantWithAnIncompleteListingCanOnlyProveMinimumAccess(bool queueOnly, ReviewStatus expected)
    {
        var queue = Bus.NamespaceResourceId + "/queues/orders";
        using var http = Http(url => url.Query.Contains("page=2") ? new(HttpStatusCode.Forbidden) : Json(new {
            value = new[] { new { properties = new { principalId = Binding.ObjectId, scope = queue,
                roleDefinitionId = "/providers/Microsoft.Authorization/roleDefinitions/69a216fc-b8fb-44d8-bc22-1f3c2cd27a39" } } },
            nextLink = url.GetLeftPart(UriPartial.Path) + "?page=2"
        }));
        var provider = new ServiceBusReviewProvider(new(http, Mock.Of<IAzureIntegrationManagerService>(), Factory()));
        var input = new ReviewInput("servicebus", Bus.Id, "data-role", null, new() { ["identityId"] = Identity.Id.ToString(), ["queue"] = "orders", ["role"] = "Sender" });
        if (queueOnly) input.Fields["assignmentScope"] = "Queue only";
        var evidence = await provider.InspectAsync(State, input, default);
        Assert.Equal(expected, evidence.Status);
        Assert.Contains("Scope: " + queue, evidence.Details);
    }

    [Theory]
    [InlineData("lab", "Matched")]
    [InlineData("Lab", "Unknown")]
    public async Task CosmosNativeRolesRespectCaseSensitiveDatabaseScope(string database, string expected)
    {
        var def = Cosmos.AccountResourceId + "/sqlRoleDefinitions/" + Guid.NewGuid();
        using var http = Http(url => url.AbsolutePath switch {
            var p when p == def => Json(new { properties = new { roleName = "Orders Reader", permissions = new[] { new { dataActions =
                new[] { "Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/items/read" } } } } }),
            var p when p == Cosmos.AccountResourceId + "/sqlRoleAssignments" => Json(new { value = new[] { new { properties = new {
                roleDefinitionId = def, principalId = Binding.ObjectId, scope = Cosmos.AccountResourceId + "/dbs/lab/colls/orders" } } } }),
            _ => throw new InvalidOperationException("Unexpected ARM read.")
        });
        var provider = new CosmosDbReviewProvider(new(http, Mock.Of<IAzureIntegrationManagerService>(), Factory()));
        var input = new ReviewInput("cosmosdb", Cosmos.Id, "data-role", null, new() { ["identityId"] = Identity.Id.ToString(),
            ["database"] = database, ["container"] = "orders", ["action"] = "items/read" });
        Assert.Null(provider.Validate(State, input));
        Assert.Equal(expected, (await provider.InspectAsync(State, input, default)).Status.ToString());
    }

    [Theory]
    [InlineData("kind = 'order'", ReviewStatus.Matched)]
    [InlineData("kind = 'audit'", ReviewStatus.Drift)]
    public async Task ObservesExactTopicRule(string expression, ReviewStatus expected)
    {
        using var http = Http(url =>
        {
            Assert.Equal(Bus.NamespaceResourceId + "/topics/events/subscriptions/orders/rules/order", url.AbsolutePath);
            return Json(new { properties = new { filterType = "SqlFilter", sqlFilter = new { sqlExpression = "kind = 'order'" } } });
        });
        var provider = new ServiceBusReviewProvider(new(http, Mock.Of<IAzureIntegrationManagerService>(), Factory()));
        var input = new ReviewInput("servicebus", Bus.Id, "subscription-filter", null, new() { ["topic"] = "events", ["subscription"] = "orders", ["rule"] = "order", ["filter"] = expression });
        Assert.Null(provider.Validate(State, input));
        Assert.Equal(expected, (await provider.InspectAsync(State, input, default)).Status);
    }

    [Theory]
    [InlineData("order", "user.Kind = 'order'", ReviewStatus.Matched)]
    [InlineData("Order", "user.Kind = 'order'", ReviewStatus.Drift)]
    [InlineData("order", "kind LIKE 'ord%'", ReviewStatus.Unknown)]
    public async Task FanOutReportsEachNamedRuleAndKeepsUnsupportedFiltersUnknown(string example, string expression, ReviewStatus expected)
    {
        using var http = Http(url =>
        {
            Assert.StartsWith(Bus.NamespaceResourceId + "/topics/events/subscriptions/", url.AbsolutePath);
            var all = Uri.UnescapeDataString(url.AbsolutePath).EndsWith("all/rules/$Default");
            Assert.True(all || url.AbsolutePath.EndsWith("orders/rules/order"));
            return Json(new { properties = new { sqlFilter = new { sqlExpression = all ? "1=1" : expression } } });
        });
        var provider = new ServiceBusReviewProvider(new(http, Mock.Of<IAzureIntegrationManagerService>(), Factory()));
        var input = new ReviewInput("servicebus", Bus.Id, "topic-fan-out", null, new() { ["topic"] = "events",
            ["rules"] = """{"all/$Default":true,"orders/order":true}""", ["property"] = "kind", ["value"] = example });
        Assert.Null(provider.Validate(State, input));
        var result = await provider.InspectAsync(State, input, default);
        Assert.Equal(expected, result.Status);
        Assert.Equal(2, result.Details.Count);
    }

    [Fact]
    public async Task IndexPathSetsIgnoreOrderingInsideDeclarationsButPreserveCompositeIndexOrder()
    {
        var seen = new { id = "catalog", indexingPolicy = new { includedPaths = new[] { new { path = "/a/*" }, new { path = "/b/*" } },
            compositeIndexes = new[] { new[] { new { path = "/a", order = "ascending" }, new { path = "/b", order = "descending" } } } } };
        using var http = Http(url =>
        {
            Assert.Equal(Cosmos.AccountResourceId + "/sqlDatabases/lab/containers/catalog", url.AbsolutePath);
            return Json(new { properties = new { resource = seen } });
        });
        var provider = new CosmosDbReviewProvider(new(http, Mock.Of<IAzureIntegrationManagerService>(), Factory()));
        var input = new ReviewInput("cosmosdb", Cosmos.Id, "container-policy", null, new() { ["database"] = "lab", ["container"] = "catalog", ["indexingPolicy"] =
            """{"includedPaths":[{"path":"/b/*"},{"path":"/a/*"}],"compositeIndexes":[[{"order":"ascending","path":"/a"},{"order":"descending","path":"/b"}]]}""" });
        Assert.Null(provider.Validate(State, input));
        Assert.Equal(ReviewStatus.Matched, (await provider.InspectAsync(State, input, default)).Status);
        input.Fields["indexingPolicy"] = """{"compositeIndexes":[[{"path":"/b","order":"descending"},{"path":"/a","order":"ascending"}]]}""";
        Assert.Equal(ReviewStatus.Drift, (await provider.InspectAsync(State, input, default)).Status);
    }

    [Fact]
    public async Task ProfileIsScopedProtectedAndPreservedByLegacyUpdates()
    {
        var store = new InMemoryFuseStore(State with { ServiceBusIntegrations = [], MessageBrokers =
            [new(Bus.MessageBrokerId, "Bus", null, "Azure Service Bus", Guid.NewGuid(), null, [], default, default)] });
        var protector = new Mock<IAzureProfileProtector>();
        protector.Setup(p => p.Protect(It.IsAny<Guid>(), "secret-fixture")).Returns("protected-fixture");
        var profiles = new AzureCredentialProfiles(store, protector.Object, new FakeAuditService(), new FakeCurrentUser());
        var command = new SaveAzureProfile("Bus observer", Bus.NamespaceResourceId, Guid.NewGuid(), Guid.NewGuid(), "secret-fixture");
        var profile = (await profiles.Save(null, command, default)).Value!;
        Assert.DoesNotContain("secret-fixture", JsonSerializer.Serialize(await profiles.ListAsync(default)));
        Assert.Equal("protected-fixture", Assert.Single(store.Current!.AzureIntegrationManager!.CredentialProfiles!).ProtectedSecret);
        Assert.False((await profiles.Save(profile.Id, command with { ResourceId = Cosmos.AccountResourceId }, default)).IsSuccess);
        var service = new ServiceBusIntegrationService(store, new(Http(_ => throw new Exception()), Mock.Of<IAzureIntegrationManagerService>()),
            new FakeAuditService(), new FakeCurrentUser());
        var item = (await service.CreateAsync(new("Bus", Bus.MessageBrokerId, Bus.NamespaceResourceId, SecretProviderAuthMode.ClientSecret, profile.Id))).Value!;
        var update = new UpdateServiceBusIntegration(item.Id, "Updated", item.MessageBrokerId, item.NamespaceResourceId, item.AuthMode);
        Assert.Equal(profile.Id, (await service.UpdateAsync(update)).Value!.CredentialProfileId);
        Assert.False((await profiles.Remove(profile.Id, default)).IsSuccess);
        Assert.Null((await service.UpdateAsync(update with { UseSharedCredential = true })).Value!.CredentialProfileId);
        Assert.True((await profiles.Remove(profile.Id, default)).IsSuccess);
    }

    [Fact]
    public void ReferenceImpactHonorsVersionKeyScopeAndAccountPermission()
    {
        var vault = Guid.NewGuid();
        var config = Guid.NewGuid();
        var pinned = new SecretBinding(SecretBindingKind.AzureKeyVault, null, new(vault, "orders", "v1"));
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), TargetKind.DataStore, AuthKind.UserPassword, pinned,
            "db_reader", null, [], [], default, default);
        var instance = new ApplicationInstance(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null,
            [new(Guid.NewGuid(), account.TargetId, TargetKind.DataStore, null, DependencyAuthKind.Account, account.Id, null)],
            [], default, default, pinned, config, ":prod");
        var app = new Application(Guid.NewGuid(), "Orders", null, null, null, null, null, null, null, [], [instance], [], default, default);
        var snapshot = State with { Applications = [app], Accounts = [account] };
        var rows = SecretReferenceConsumers.Find(snapshot, vault, "orders", true, config, "connection:prod", "v2");
        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, r => r.Source.StartsWith("Dependency account") && r.PinnedVersion == "v1");
        Assert.Contains(rows, r => r.Source.StartsWith("Configured key") && r.PinnedVersion == "v2");
        var redacted = Assert.Single(SecretReferenceConsumers.Find(snapshot, vault, "orders", false, config, "connection:dev", "v2"));
        Assert.Equal("Instance API key", redacted.Source);
    }

    [Fact]
    public async Task WorkloadReturnsOnlyCredentialMetadataAndPreservesPrincipalIfApplicationDenied()
    {
        var profile = new AzureCredentialProfile(Guid.NewGuid(), "Graph", Bus.NamespaceResourceId, Binding.TenantId, Guid.NewGuid(), "protected", true);
        var manager = new Mock<IAzureIntegrationManagerService>();
        manager.Setup(m => m.GetProfileCredentialsAsync(profile.Id, profile.ResourceId)).ReturnsAsync(new SecretProviderCredentials(Binding.TenantId.ToString(), profile.ClientId.ToString(), "private"));
        var key = Guid.NewGuid();
        var app = Guid.NewGuid();
        using var http = Http(url =>
        {
            Assert.Equal("graph.microsoft.com", url.Host);
            if (url.AbsolutePath.Contains("applications(")) return new(HttpStatusCode.Forbidden);
            Assert.Equal("/v1.0/servicePrincipals/" + Binding.ObjectId, url.AbsolutePath);
            return Json(new { id = Binding.ObjectId, appId = app, displayName = "Lab reader", servicePrincipalType = "Application", accountEnabled = true,
                keyCredentials = Array.Empty<object>(), passwordCredentials = new[] { new {keyId = key, endDateTime = "2030-01-01T00:00:00Z", secretText = "never-project", hint = "never-project" } } });
        });
        var inspector = new WorkloadInspector(http, manager.Object, Factory());
        var observation = await inspector.Inspect(profile, Binding, default);
        Assert.Equal("visible", observation.PrincipalVisibility);
        Assert.Equal("denied", observation.ApplicationVisibility);
        Assert.Equal(key, Assert.Single(observation.Credentials).KeyId);
        Assert.DoesNotContain("never-project", JsonSerializer.Serialize(observation));
        Assert.Equal("profile-out-of-scope", (await inspector.Inspect(profile with { DirectoryLookup = false }, Binding, default)).PrincipalVisibility);
        Assert.Equal("profile-out-of-scope", (await inspector.Inspect(profile, Binding with { TenantId = Guid.NewGuid() }, default)).PrincipalVisibility);
        manager.Verify(m => m.GetProfileCredentialsAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Once);
    }
}
