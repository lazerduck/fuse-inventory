using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Fuse.Core.Areas.CosmosDbIntegration;
using Fuse.Core.Models;
using Fuse.Core.Configs;
using Fuse.Data.Stores;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class CosmosDbIntegrationTests
{
    private const string Id = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/test/providers/Microsoft.DocumentDB/databaseAccounts/test-account";
    private static readonly CosmosDbConnection Saved = new(Guid.NewGuid(), "Lab", Guid.NewGuid(), Id, SecretProviderAuthMode.ManagedIdentity, DateTime.UtcNow, DateTime.UtcNow);
    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    private static object Row(object resource) => new { properties = new { resource, internalMetadata = "omit-this" } };
    private static object Database(string id) => Row(new { id });

    private static CosmosDbInspector Inspector(Func<Uri, HttpResponseMessage> response)
    {
        var factory = new Mock<ITokenCredentialFactory>();
        factory.Setup(x => x.Create(SecretProviderAuthMode.ManagedIdentity, null)).Returns(new Credential());
        return new(new HttpClient(new Handler(response)), Mock.Of<IAzureIntegrationManagerService>(), factory.Object);
    }

    [Theory]
    [InlineData("https://other.example/sqlDatabases")]
    [InlineData("https://management.azure.com" + Id + "/sqlDatabases/Lab/containers?$skip=1")]
    public async Task ContainerPagingKeepsObservedMetadataAndRejectsCrossScopeLinks(string link)
    {
        var inspector = Inspector(url =>
        {
            if (url.AbsolutePath == Id + "/sqlDatabases/lab/throughputSettings/default") return new(HttpStatusCode.Forbidden);
            Assert.Equal(Id + "/sqlDatabases/lab/containers", url.AbsolutePath);
            return Json(new { value = new[] { Row(new { id = "orders" }) }, nextLink = link });
        });
        var result = await inspector.DiscoverAsync(Saved, "lab", null, default);
        Assert.Equal("invalid-continuation", result.Visibility);
        Assert.Equal("orders", Assert.Single(Assert.Single(result.Databases).Containers).Name);
    }

    [Fact]
    public async Task KeepsDistinctCaseSensitiveDatabasesAndPerDatabaseVisibility()
    {
        var inspector = Inspector(url => url.AbsolutePath switch
        {
            Id + "/sqlDatabases" => Json(new { value = new[] { Database("Lab"), Database("lab") } }),
            Id + "/sqlDatabases/Lab/containers" => Json(new { value = Array.Empty<object>() }),
            Id + "/sqlDatabases/lab/containers" => new(HttpStatusCode.Forbidden),
            var path when path.EndsWith("/throughputSettings/default") => new(HttpStatusCode.Forbidden),
            _ => throw new Exception("Unexpected request")
        });
        var result = await inspector.DiscoverAsync(Saved, null, null, default);
        Assert.Equal("visible", result.Visibility);
        Assert.Equal(new[] { ("Lab", "none-observed"), ("lab", "denied") }, result.Databases.Select(d => (d.Name, d.ContainerVisibility)));
    }

    [Fact]
    public async Task ContainerReaderCanInspectOneNamedContainerWhenParentListingIsDenied()
    {
        var inspector = Inspector(url => url.AbsolutePath == Id + "/sqlDatabases/lab/containers/catalog"
            ? Json(Row(new { id = "catalog", partitionKey = new { kind = "MultiHash", version = 2, paths = new[] { "/tenantId", "/category" } },
                indexingPolicy = new { automatic = true, indexingMode = "consistent", includedPaths = new[] { new { path = "/*" } },
                    excludedPaths = new[] { new { path = "/internal/*" } },
                    compositeIndexes = new[] { new[] { new { path = "/category", order = "ascending" }, new { path = "/date", order = "descending" } } } } }))
            : new(HttpStatusCode.Forbidden));
        Assert.Equal("denied", (await inspector.ValidateAsync(Id, SecretProviderAuthMode.ManagedIdentity, default)).Status);
        var direct = await inspector.DiscoverAsync(Saved, "lab", "catalog", default);
        Assert.Equal("direct", direct.Visibility);
        var row = Assert.Single(Assert.Single(direct.Databases).Containers);
        Assert.Equal(new[] { "/tenantId", "/category" }, row.PartitionKey!.Paths);
        Assert.Equal("MultiHash", row.PartitionKey.Kind);
        Assert.Equal(2, row.PartitionKey.Version);
        Assert.Equal("consistent", row.IndexingPolicy!.Mode);
        Assert.Equal("/internal/*", Assert.Single(row.IndexingPolicy.ExcludedPaths));
        Assert.True(row.IndexingPolicy.Automatic);
        using var policy = JsonDocument.Parse(row.IndexingPolicy.PolicyJson);
        Assert.Equal("descending", policy.RootElement.GetProperty("compositeIndexes")[0][1].GetProperty("order").GetString());
        Assert.DoesNotContain("omit-this", JsonSerializer.Serialize(direct));
        var hidden = await inspector.DiscoverAsync(Saved, "lab", "customers", default);
        Assert.Equal("denied", hidden.Visibility);
        Assert.Empty(hidden.Databases);
    }

    [Fact]
    public async Task ScopedConfigurationSurvivesReloadAndImport()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fuse-cosmos-" + Guid.NewGuid());
        try
        {
            using var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = directory });
            var environment = new EnvironmentInfo(Guid.NewGuid(), "Lab", null, []);
            var dataStore = new DataStore(Saved.DataStoreId, "Catalog", null, "Azure Cosmos DB", environment.Id, null, null, [], DateTime.UtcNow, DateTime.UtcNow);
            await store.UpdateAsync(s => s with { Environments = [environment], DataStores = [dataStore] });
            var audit = new FakeAuditService();
            var service = new CosmosDbIntegrationService(store, Inspector(_ => throw new Exception("Save must be offline.")), audit, new FakeCurrentUser());
            var saved = (await service.CreateAsync(new("Cosmos", dataStore.Id, Id, SecretProviderAuthMode.ClientSecret))).Value!;
            Assert.False((await service.CreateAsync(new("Duplicate", dataStore.Id, Id, SecretProviderAuthMode.ClientSecret))).IsSuccess);
            Assert.False((await service.UpdateAsync(new(saved.Id, "Cosmos", dataStore.Id, Id + "/sqlDatabases", SecretProviderAuthMode.ClientSecret))).IsSuccess);
            Assert.False((await service.DiscoverAsync(saved.Id, "..", null)).IsSuccess);
            Assert.False((await service.DiscoverAsync(saved.Id, null, "orders")).IsSuccess);
            Assert.False((await service.DiscoverAsync(saved.Id, "lab", "../other")).IsSuccess);
            var config = new ConfigService(store, audit, Mock.Of<Fuse.Core.Areas.Logging.ILogService>(), new FakeCurrentUser());
            await config.ImportAsync(await config.ExportAsync(ConfigFormat.Json), ConfigFormat.Json);
            Assert.Equal(saved, Assert.Single((await store.LoadAsync()).CosmosDbIntegrations));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class Handler(Func<Uri, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("management.azure.com", request.RequestUri!.Host);
            Assert.Equal("lab-token", request.Headers.Authorization?.Parameter);
            return Task.FromResult(response(request.RequestUri));
        }
    }

    private sealed class Credential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken ct) => new("lab-token", DateTimeOffset.UtcNow.AddHours(1));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken ct) => ValueTask.FromResult(GetToken(requestContext, ct));
    }
}
