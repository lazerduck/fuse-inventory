using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Fuse.Core.Areas.ServiceBusIntegration;
using Fuse.Core.Models;
using Fuse.Core.Configs;
using Fuse.Data.Stores;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class ServiceBusIntegrationTests
{
    private const string Id = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/test/providers/Microsoft.ServiceBus/namespaces/test-bus";
    private static readonly ServiceBusConnection Saved = new(Guid.NewGuid(), "Lab", Guid.NewGuid(), Id, SecretProviderAuthMode.ManagedIdentity, DateTime.UtcNow, DateTime.UtcNow);
    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    private static object Queue(string name) => new { id = Id + "/queues/" + name, name, properties = new { status = "Active" } };

    private static ServiceBusInspector Inspector(Func<Uri, HttpResponseMessage> response)
    {
        var factory = new Mock<ITokenCredentialFactory>();
        factory.Setup(x => x.Create(SecretProviderAuthMode.ManagedIdentity, null)).Returns(new Credential());
        return new(new HttpClient(new Handler(response)), Mock.Of<IAzureIntegrationManagerService>(), factory.Object);
    }

    [Theory]
    [InlineData("https://other.example/")]
    [InlineData("https://management.azure.com/subscriptions/another/queues")]
    public async Task KeepsVisiblePagesWhenContinuationLeavesTheCollection(string continuation)
    {
        var inspector = Inspector(url =>
        {
            if (url.AbsolutePath == Id + "/queues") return Json(new { value = new[] { Queue("orders") }, nextLink = continuation });
            Assert.Contains(url.AbsolutePath, new[] { Id + "/authorizationRules", Id + "/queues/orders/authorizationRules" });
            return new(HttpStatusCode.Forbidden);
        });
        var result = await inspector.DiscoverAsync(Saved, [], default);
        Assert.Equal("invalid-continuation", result.QueueVisibility);
        Assert.Equal("denied", Assert.Single(result.Queues).AuthorizationRules.Visibility);
        Assert.Equal("denied", result.NamespaceAuthorizationRules.Visibility);
    }

    [Fact]
    public async Task QueueReaderCanSeeKnownQueueWhenListingIsDenied()
    {
        var inspector = Inspector(url =>
        {
            if (url.AbsolutePath == Id + "/queues/orders") return Json(Queue("orders"));
            if (url.AbsolutePath == Id + "/queues/orders/authorizationRules")
                return Json(new { value = new[] { new { name = "send", properties = new { rights = new[] { "Send" }, primaryKey = "must-never-leave" } } } });
            return new(HttpStatusCode.Forbidden);
        });
        var result = await inspector.DiscoverAsync(Saved, [new(Guid.NewGuid(), "orders", null), new(Guid.NewGuid(), "audit", null)], default);
        Assert.Equal("denied", result.QueueVisibility);
        Assert.Equal("observed", result.Inventory[0].Status);
        Assert.Equal("unknown", result.Inventory[1].Status);
        Assert.Equal("Send", Assert.Single(Assert.Single(Assert.Single(result.Queues).AuthorizationRules.Policies).Rights));
        Assert.DoesNotContain("must-never-leave", JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task RetainsEmptyVisiblePageAndFollowsValidPaging()
    {
        var inspector = Inspector(url =>
        {
            if (url.AbsolutePath == Id + "/queues")
            {
                if (url.Query.Contains("skip")) return Json(new { value = new[] { Queue("second") } });
                return Json(new { value = Array.Empty<object>(), nextLink = "https://management.azure.com" + Id + "/queues?$skip=1" });
            }
            return Json(new { value = Array.Empty<object>() });
        });
        var result = await inspector.DiscoverAsync(Saved, [], default);
        Assert.Equal("visible", result.QueueVisibility);
        Assert.Equal("second", Assert.Single(result.Queues).Name);
        Assert.Empty(result.NamespaceAuthorizationRules.Policies);
        Assert.Equal("visible", result.NamespaceAuthorizationRules.Visibility);
    }

    [Fact]
    public async Task ScopedConfigurationSurvivesReloadAndImport()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fuse-servicebus-" + Guid.NewGuid());
        try
        {
            using var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = directory });
            var environment = new EnvironmentInfo(Guid.NewGuid(), "Lab", null, []);
            var broker = new MessageBroker(Saved.MessageBrokerId, "Bus", null, "Azure Service Bus", environment.Id, null, [], DateTime.UtcNow, DateTime.UtcNow);
            await store.UpdateAsync(s => s with { Environments = [environment], MessageBrokers = [broker] });
            var audit = new FakeAuditService();
            var service = new ServiceBusIntegrationService(store, Inspector(_ => throw new Exception("Save must be offline.")), audit, new FakeCurrentUser());
            var saved = (await service.CreateAsync(new("Bus", broker.Id, Id, SecretProviderAuthMode.ClientSecret))).Value!;
            Assert.False((await service.CreateAsync(new("Duplicate", broker.Id, Id, SecretProviderAuthMode.ClientSecret))).IsSuccess);
            Assert.False((await service.UpdateAsync(new(saved.Id, "Bus", broker.Id, Id + "/queues", SecretProviderAuthMode.ClientSecret))).IsSuccess);
            var config = new ConfigService(store, audit, Mock.Of<Fuse.Core.Areas.Logging.ILogService>(), new FakeCurrentUser());
            await config.ImportAsync(await config.ExportAsync(ConfigFormat.Json), ConfigFormat.Json);
            Assert.Equal(saved, Assert.Single((await store.LoadAsync()).ServiceBusIntegrations));
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
