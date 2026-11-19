using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fuse.Core.Areas.KeycloakIntegration;
using Fuse.Core.Commands;
using Fuse.Core.Configs;
using Fuse.Core.Models;
using Fuse.Data.Stores;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class KeycloakIntegrationTests
{
    private static readonly Uri Issuer = new("https://sso.example.org/auth/realms/Alpha");
    private const string Secret = "private-inspector-credential";

    [Fact]
    public async Task SecretIsProtectedAndCannotBeReusedAtAnotherRealmOrClient()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"keycloak-store-{Guid.NewGuid():N}");
        try
        {
            using var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = directory });
            var protector = new Mock<IKeycloakSecretProtector>(MockBehavior.Strict);
            protector.Setup(p => p.Protect(Secret)).Returns("protected-value");
            var audit = new FakeAuditService();
            using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("Saving must be offline.")));
            var service = new KeycloakIntegrationService(store, new(http), protector.Object, audit, new FakeCurrentUser());
            var saved = (await service.CreateAsync(new("test", Issuer, "observer", Secret))).Value!;
            Assert.True((await service.UpdateAsync(new(saved.Id, "new name", Issuer, "observer", null))).IsSuccess);
            Assert.False((await service.UpdateAsync(new(saved.Id, "changed realm", new("https://other.example/realms/Alpha"), "observer", null))).IsSuccess);
            Assert.False((await service.UpdateAsync(new(saved.Id, "changed client", Issuer, "another", null))).IsSuccess);
            Assert.DoesNotContain(Secret, await File.ReadAllTextAsync(Path.Combine(directory, "keycloakintegrations.json")));
            var config = new ConfigService(store, audit, Mock.Of<Fuse.Core.Areas.Logging.ILogService>(), new FakeCurrentUser());
            var exported = await config.ExportAsync(ConfigFormat.Json);
            Assert.DoesNotContain("protected-value", exported);
            await config.ImportAsync(exported, ConfigFormat.Json);
            Assert.Equal("protected-value", Assert.Single((await store.LoadAsync()).KeycloakIntegrations).ProtectedClientSecret);
            Assert.DoesNotContain(Secret, JsonSerializer.Serialize(await service.GetAllAsync()));
            Assert.DoesNotContain(Secret, JsonSerializer.Serialize(audit.Logs));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("https://user:pass@sso.example/realms/Alpha")]
    [InlineData("https://sso.example/realms/A%2FB")]
    [InlineData("https://sso.example/realms/../realms/Alpha")]
    [InlineData("https://sso.example/realms/Alpha?token=x")]
    [InlineData("file:///realms/Alpha")]
    public async Task RejectsInvalidIssuer(string issuer)
    {
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("Invalid issuer contacted.")));
        var service = new KeycloakIntegrationService(new InMemoryFuseStore(), new(http),
            Mock.Of<IKeycloakSecretProtector>(), new FakeAuditService(), new FakeCurrentUser());
        Assert.False((await service.ValidateAsync(new(new Uri(issuer), "observer", Secret))).IsSuccess);
    }

    [Fact]
    public async Task FilteredSuccessfulEnumerationDoesNotProveAClientMissing()
    {
        var store = new InMemoryFuseStore();
        var account = new Account(Guid.NewGuid(), Guid.NewGuid(), TargetKind.Application, AuthKind.OAuthClient,
            new(SecretBindingKind.None, null, null), null, new() { ["keycloakIssuer"] = Issuer.AbsoluteUri, ["keycloakClientId"] = "absent" },
            [], [], DateTime.UtcNow, DateTime.UtcNow);
        await store.UpdateAsync(s => s with { Accounts = [account] });
        var requests = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            requests++;
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal(Issuer.AbsoluteUri + "/protocol/openid-connect/token", request.RequestUri!.AbsoluteUri);
                return JsonContent.Create(new { access_token = "opaque-token" });
            }
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/auth/admin/realms/Alpha/clients", request.RequestUri!.AbsolutePath);
            Assert.Equal("opaque-token", request.Headers.Authorization?.Parameter);
            return JsonContent.Create(Array.Empty<object>());
        }));
        var result = await new KeycloakInspector(http).DiscoverAsync(new(Issuer, "observer", Secret), await store.GetAsync(), default);
        Assert.Equal("limited", result.ClientsStatus);
        Assert.Equal("unknown", Assert.Single(result.Accounts).Status);
        Assert.Equal(2, requests);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpContent> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = respond(request) });
    }
}
