using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Areas.KeycloakIntegration;
using Xunit;

namespace Fuse.Tests.Services;

public class KeycloakReviewTests
{
    private sealed class Realm(bool scoped, bool standardClaim, bool denial) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            if (request.Method == HttpMethod.Post) {
                Assert.Equal("/realms/alpha/protocol/openid-connect/token", path);
                return Json(new { access_token = "opaque" });
            }
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.StartsWith("/admin/realms/alpha/", path);
            path = path["/admin/realms/alpha/".Length..];
            return path switch
            {
                "clients" => Json(new[] { new { id = "id", clientId = "worker" } }),
                "clients/id" => Json(new { id = "id", clientId = "worker", serviceAccountsEnabled = true,
                    redirectUris = new[] { "https://example.org/b", "https://example.org/a" } }),
                "clients/id/service-account-user" => Json(new { id = "user" }),
                "users/user/role-mappings" => Json(new {realmMappings = new[] {new {id="editor",name="editor",composite=true}}}),
                "roles-by-id/editor/composites" or "users/user/role-mappings/realm/composite" =>
                    Json(new[] { new {id="viewer",name="viewer",composite=false,clientRole=false} }),
                "clients/id/evaluate-scopes/scope-mappings/alpha/granted" => denial
                    ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))
                    : Json(scoped ? new[] { new {id="viewer"} } : []),
                "clients/id/evaluate-scopes/generate-example-access-token" => Json(new {
                    realm_access = new {roles = standardClaim ? new[] {"viewer"} : []}, privateMapperOutput="do-not-persist"
                }),
                _ => throw new Exception("Unexpected request for " + path)
            };
        }
        private static Task<HttpResponseMessage> Json(object value) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=JsonContent.Create(value)});
    }

    [Theory]
    [InlineData(true, true, false, ReviewStatus.Matched)]
    [InlineData(false, true, false, ReviewStatus.Drift)]
    [InlineData(true, false, false, ReviewStatus.Unknown)]
    [InlineData(true, true, true, ReviewStatus.Unknown)]
    public async Task SeparatesCompositeGrantFromTokenScopeAndMapper(bool scoped, bool claim, bool deny, ReviewStatus expected)
    {
        using var transport = new Realm(scoped, claim, deny);
        using var http = new HttpClient(transport);
        var review = new ReviewInput("keycloak", Guid.NewGuid(), "role", null,
            new() {["client"]="worker", ["role"]="viewer", ["basis"]="token-eligible", ["expected"]="present"});
        var result = await new KeycloakInspector(http).ReviewAsync(new(new Uri("http://kc/realms/alpha"), "observer", "private"), review, default);
        Assert.Equal(expected, result.Status);
        Assert.Contains("Service account → realm/editor → realm/viewer", result.Details);
        Assert.DoesNotContain("do-not-persist", JsonSerializer.Serialize(result));
        Assert.Equal(scoped && !deny, transport.Paths.Any(s => s.EndsWith("generate-example-access-token")));
    }

    [Fact]
    public async Task RedirectReviewUsesExactSetEquality()
    {
        using var http = new HttpClient(new Realm(false, false, false));
        var review = new ReviewInput("keycloak", Guid.NewGuid(), "client-setting", null,
            new() {["client"]="worker", ["setting"]="redirectUris", ["expected"]="https://example.org/a\nhttps://example.org/b"});
        var inspector = new KeycloakInspector(http);
        var settings = new Fuse.Core.Commands.ValidateKeycloakConnection(new("http://kc/realms/alpha"), "observer", "private");
        Assert.Equal(ReviewStatus.Matched, (await inspector.ReviewAsync(settings, review, default)).Status);
        review.Fields["expected"] = "https://example.org/b";
        var diff = await inspector.ReviewAsync(settings, review, default);
        Assert.Equal(ReviewStatus.Drift, diff.Status);
        Assert.Contains("Unexpected: https://example.org/a", diff.Details);
    }
}
