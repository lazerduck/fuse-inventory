using System.Net;
using System.Text.Json;
using Azure.Core;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class AzureDiscoveryClientTests
{
    private const string Subscription = "00000000-0000-0000-0000-000000000001";

    private static AzureDiscoveryClient Client(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
    {
        var manager = new Mock<IAzureIntegrationManagerService>();
        var credentials = new Mock<ITokenCredentialFactory>();
        credentials.Setup(f => f.Create(It.IsAny<SecretProviderAuthMode>(), It.IsAny<SecretProviderCredentials?>())).Returns(new TestCredential());
        return new AzureDiscoveryClient(new HttpClient(new Handler(send)), manager.Object, credentials.Object);
    }

    [Fact]
    public async Task SubscriptionPagination_RejectsForeignNextLinkWithoutSendingCredentials()
    {
        var calls = 0;
        var client = Client(request =>
        {
            calls++;
            Assert.Equal("management.azure.com", request.RequestUri!.Host);
            return Task.FromResult(Json(new { value = new[] { new { subscriptionId = Subscription, displayName = "Production" } }, nextLink = "https://example.com/steal" }));
        });
        var result = await client.ListSubscriptionsAsync(SecretProviderAuthMode.ManagedIdentity, default);
        Assert.Equal(1, calls);
        Assert.Single(result.Subscriptions);
        Assert.Contains(result.Warnings, w => w.Contains("incomplete"));
    }

    [Fact]
    public async Task Discovery_FollowsSkipTokens_PreservesSuccessfulSubscriptionsOnForbidden()
    {
        var calls = 0;
        var client = Client(async request =>
        {
            calls++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var options = body.RootElement.GetProperty("options");
            if (calls == 1)
            {
                Assert.False(options.TryGetProperty("$skipToken", out _));
                return Json(new Dictionary<string, object> { ["data"] = new[] { Resource("first") }, ["$skipToken"] = "continuation" });
            }
            if (calls == 2)
            {
                Assert.Equal("continuation", options.GetProperty("$skipToken").GetString());
                return Json(new { data = new[] { Resource("second") } });
            }
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        });
        var response = await client.DiscoverAsync(new(SecretProviderAuthMode.ManagedIdentity,
            [Subscription, "00000000-0000-0000-0000-000000000002"]), default);
        Assert.Equal(3, calls);
        Assert.Equal(2, response.Resources.Count);
        Assert.Contains(response.Warnings, w => w.Contains("denied"));
    }

    [Fact]
    public async Task Discovery_RejectsNonAzureAndMalformedEndpoints()
    {
        var client = Client(_ => Task.FromResult(Json(new { data = new[]
        {
            Resource("first", "https://example.com/"), Resource("second", "https://demo.vault.azure.net.evil.test/"),
            Resource("third", "https://user:pass@demo.vault.azure.net/"), Resource("valid")
        }})));
        var response = await client.DiscoverAsync(new(SecretProviderAuthMode.ManagedIdentity, [Subscription]), default);
        Assert.Equal("valid", Assert.Single(response.Resources).Name);
        Assert.NotEmpty(response.Warnings);
    }

    [Fact]
    public async Task ClientSecretDiscovery_RequiresSharedCredentials()
    {
        var client = Client(_ => throw new Xunit.Sdk.XunitException("Should not contact Azure"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListSubscriptionsAsync(SecretProviderAuthMode.ClientSecret, default));
        Assert.Contains("shared Azure credentials", error.Message);
    }

    private static object Resource(string name, string? endpoint = null) => new
    {
        id = $"/subscriptions/{Subscription}/resourceGroups/test/providers/Microsoft.KeyVault/vaults/{name}",
        name, type = "microsoft.keyvault/vaults", subscriptionId = Subscription, resourceGroup = "test", location = "uksouth",
        endpoint = endpoint ?? $"https://{name}.vault.azure.net/"
    };
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("test-token", DateTimeOffset.UtcNow.AddHours(1));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
