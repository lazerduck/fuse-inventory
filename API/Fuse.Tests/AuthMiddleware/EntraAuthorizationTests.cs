using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.API.Entra;
using Fuse.Core.Areas.Security.Entra;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Generated = Fuse.Tests.ApiClient;

namespace Fuse.Tests.AuthMiddleware;

[Collection("ApiAuthCollection")]
public class EntraAuthorizationTests(ApiIntegrationFixture fixture) : IAsyncLifetime
{
    private IServiceScope _scope = null!;
    private IFuseStore _store = null!;
    private Snapshot _original = null!;
    private IFuseUserService _users = null!;
    private IFuseUserSessionService _sessions = null!;
    private IFuseRoleService _roles = null!;
    private IEntraIdentityService _identities = null!;
    private HttpClient _adminClient = null!;
    private Generated.FuseApiClient _adminApi = null!;
    private FuseUser _admin = null!;
    private FuseRole _role = null!;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _group = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _scope = fixture.Services.CreateScope();
        _store = _scope.ServiceProvider.GetRequiredService<IFuseStore>();
        _original = await _store.GetAsync();
        _users = _scope.ServiceProvider.GetRequiredService<IFuseUserService>();
        _sessions = _scope.ServiceProvider.GetRequiredService<IFuseUserSessionService>();
        _roles = _scope.ServiceProvider.GetRequiredService<IFuseRoleService>();
        _identities = _scope.ServiceProvider.GetRequiredService<IEntraIdentityService>();
        _admin = (await _users.CreateUser("admin-entra-test", "test-password", true, [])).Value!;
        var token = (await _sessions.CreateSession(_admin)).Value!;
        _adminClient = fixture.CreateAuthenticatedHttpClient(token);
        _adminApi = new Generated.FuseApiClient(fixture.BaseUrl, _adminClient);
        await _adminApi.ApiEntraSettingsPutAsync(SettingsCommand());
        _role = (await _roles.CreateRole("Mapped reader", "", ["tags:read"], [new(_group, "Engineering")])).Value!;
    }

    public async Task DisposeAsync()
    {
        await _store.SaveAsync(_original);
        _adminClient.Dispose();
        _scope.Dispose();
    }

    [Fact]
    public async Task SettingsRequirePermissionAndNeverExposeTheStoredSecret()
    {
        using var anonymous = fixture.CreateUnauthenticatedHttpClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/entra/settings")).StatusCode);

        var settings = await _adminApi.ApiEntraSettingsGetAsync();
        Assert.True(settings.HasClientSecret);
        var json = await _adminClient.GetStringAsync("/api/entra/settings");
        Assert.DoesNotContain("test-only-secret", json);
        Assert.DoesNotContain("protectedClientSecret", json);
        Assert.Equal(SecurityPosture.FullyRestricted, (await _store.GetAsync()).SecurityContext.Posture);

        var command = SettingsCommand();
        command.PublicOrigin = "http://public.example.com";
        var error = await Assert.ThrowsAnyAsync<Generated.ApiException>(() => _adminApi.ApiEntraSettingsPutAsync(command));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task FirstEntraUserAndSessionPersistAcrossStoreReload()
    {
        var session = await SignInMappedUser();
        var user = (await _users.GetUser(session.UserId)).Value!;
        Assert.Empty(user.PasswordHash);
        Assert.False(user.IsAdmin);

        await _store.LoadAsync();

        Assert.True((await _sessions.ValidateSession(session.Token)).IsSuccess);
        Assert.Equal(user.EntraObjectId, (await _users.GetUser(user.Id)).Value!.EntraObjectId);
    }

    [Fact]
    public async Task MicrosoftSessionGetsInheritedPermissionsButPasswordSessionDoesNot()
    {
        var session = await SignInMappedUser(linkLocalAccount: true);
        using var microsoftClient = fixture.CreateAuthenticatedHttpClient(session.Token);
        var user = (await _users.GetUser(session.UserId)).Value!;
        using var passwordClient = fixture.CreateAuthenticatedHttpClient((await _sessions.CreateSession(user)).Value!);

        Assert.Equal(HttpStatusCode.OK, (await microsoftClient.GetAsync("/api/tag")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await passwordClient.GetAsync("/api/tag")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await microsoftClient.GetAsync("/api/entra/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await microsoftClient.PostAsync($"/api/entra/users/{_admin.Id}/revoke", null)).StatusCode);

        var state = await ReadState(microsoftClient);
        Assert.Empty(state.GetProperty("currentUser").GetProperty("roleIds").EnumerateArray());
        Assert.Contains("tags:read", state.GetProperty("permissions").EnumerateArray().Select(value => value.GetString()));
        var membership = Assert.Single(state.GetProperty("currentUser").GetProperty("roleMemberships").EnumerateArray());
        Assert.False(membership.GetProperty("direct").GetBoolean());
        Assert.Single(membership.GetProperty("entraGroups").EnumerateArray());
    }

    [Fact]
    public async Task RemovingOneGrantSourcePreservesTheOther()
    {
        var session = await SignInMappedUser();
        using var client = fixture.CreateAuthenticatedHttpClient(session.Token);
        await _users.SetUserRoles(session.UserId, [_role.Id]);

        var state = await ReadState(client);
        var membership = Assert.Single(state.GetProperty("currentUser").GetProperty("roleMemberships").EnumerateArray());
        Assert.True(membership.GetProperty("direct").GetBoolean());
        Assert.Single(membership.GetProperty("entraGroups").EnumerateArray());

        await _users.SetUserRoles(session.UserId, []);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tag")).StatusCode);

        await _users.SetUserRoles(session.UserId, [_role.Id]);
        await _roles.UpdateRole(_role.Id, _role.Name, "", _role.Permissions, []);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tag")).StatusCode);

        await _users.SetUserRoles(session.UserId, []);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/tag")).StatusCode);
    }

    [Fact]
    public async Task LinkingRequiresAUserSessionAndProofOfTheLocalPassword()
    {
        var session = await SignInMappedUser(linkLocalAccount: true);
        using var client = fixture.CreateAuthenticatedHttpClient(session.Token);
        using var keyClient = await CreateApiKeyClient(session.UserId);

        var command = new { currentPassword = "test-password" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await keyClient.PostAsJsonAsync("/api/entra/link", command)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/entra/link", new { currentPassword = "wrong-password" })).StatusCode);

        var response = await client.PostAsJsonAsync("/api/entra/link", command);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("fuse-entra-link", response.Headers.GetValues("Set-Cookie").Single());
    }

    [Fact]
    public async Task DisablingUserBlocksAllCredentialsAndEnablingAllowsFreshSignIn()
    {
        var session = await SignInMappedUser(linkLocalAccount: true);
        var user = (await _users.GetUser(session.UserId)).Value!;
        using var microsoftClient = fixture.CreateAuthenticatedHttpClient(session.Token);
        using var passwordClient = fixture.CreateAuthenticatedHttpClient((await _sessions.CreateSession(user)).Value!);
        using var keyClient = await CreateApiKeyClient(user.Id);
        Assert.Equal(HttpStatusCode.OK, (await keyClient.GetAsync("/api/tag")).StatusCode);

        await _adminApi.ApiEntraUsersDisabledAsync(user.Id, new Generated.SetUserDisabled { Disabled = true });

        Assert.Equal(HttpStatusCode.Unauthorized, (await microsoftClient.GetAsync("/api/tag")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await passwordClient.GetAsync("/api/tag")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await keyClient.GetAsync("/api/tag")).StatusCode);
        Assert.False((await _users.VerifyUser(user.UserName, "test-password")).IsSuccess);
        var identity = new VerifiedEntraIdentity(_tenant, user.EntraObjectId!.Value, "Linked user", [_group], DateTime.UtcNow.AddHours(1));
        var version = (await _store.GetAsync()).SecurityContext.Entra.Version;
        Assert.False((await _identities.SignIn(identity, version)).IsSuccess);

        await _adminApi.ApiEntraUsersDisabledAsync(user.Id, new Generated.SetUserDisabled { Disabled = false });
        var next = (await _identities.SignIn(identity, version)).Value!;
        using var nextClient = fixture.CreateAuthenticatedHttpClient(next.Token);
        Assert.Equal(HttpStatusCode.OK, (await nextClient.GetAsync("/api/tag")).StatusCode);
    }

    [Fact]
    public async Task RevokingSessionsRejectsAnExistingBearerToken()
    {
        var session = await SignInMappedUser();
        using var client = fixture.CreateAuthenticatedHttpClient(session.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tag")).StatusCode);

        await _adminApi.ApiEntraUsersRevokeAsync(session.UserId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tag")).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task MissingOrForgedAntiforgeryTokenDoesNotConsumeHandoff(string? requestToken)
    {
        var session = await SignInMappedUser();
        using var client = fixture.CreateUnauthenticatedHttpClient();
        AddHandoffCookie(client, session.Token);
        if (requestToken is not null)
            client.DefaultRequestHeaders.Add("X-Fuse-Entra", requestToken);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/auth/entra/exchange", null)).StatusCode);

        await AddAntiforgeryToken(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/auth/entra/exchange", null)).StatusCode);
    }

    [Fact]
    public async Task AntiforgeryTokenMustMatchTheBrowserCookie()
    {
        var session = await SignInMappedUser();
        using var client = fixture.CreateUnauthenticatedHttpClient();
        using var otherClient = fixture.CreateUnauthenticatedHttpClient();
        AddHandoffCookie(client, session.Token);
        await AddAntiforgeryToken(client);
        var otherToken = await otherClient.GetFromJsonAsync<EntraAntiforgeryInfo>("/auth/entra/antiforgery");
        client.DefaultRequestHeaders.Remove("X-Fuse-Entra");
        client.DefaultRequestHeaders.Add("X-Fuse-Entra", otherToken!.RequestToken);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/auth/entra/exchange", null)).StatusCode);

        await AddAntiforgeryToken(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/auth/entra/exchange", null)).StatusCode);
    }

    [Fact]
    public async Task AntiforgeryTokenAloneDoesNotAuthorizeAnExchange()
    {
        using var client = fixture.CreateUnauthenticatedHttpClient();
        await AddAntiforgeryToken(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/auth/entra/exchange", null)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandoffRequiresAValidSessionAndCanOnlyBeExchangedOnce(bool revoked)
    {
        var session = await SignInMappedUser();
        using var client = fixture.CreateUnauthenticatedHttpClient();
        AddHandoffCookie(client, session.Token);
        await AddAntiforgeryToken(client);
        if (revoked)
            await _identities.Revoke(session.UserId);

        var response = await client.PostAsync("/auth/entra/exchange", null);
        Assert.Equal(revoked ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, response.StatusCode);
        if (!revoked)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(session.Token, result.GetProperty("token").GetString());
            Assert.True(response.Headers.CacheControl!.NoStore);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/auth/entra/exchange", null)).StatusCode);
    }

    private void AddHandoffCookie(HttpClient client, string sessionToken)
    {
        var tickets = _scope.ServiceProvider.GetRequiredService<EntraFlowTickets>();
        var ticket = tickets.Issue(new EntraFlowTicket(sessionToken, null, null));
        client.DefaultRequestHeaders.Add("Cookie", $"{EntraAuthentication.HandoffCookie}={ticket}");
    }

    private static async Task AddAntiforgeryToken(HttpClient client)
    {
        var response = await client.GetAsync("/auth/entra/antiforgery");
        Assert.True(response.Headers.CacheControl!.NoStore);
        var token = await response.Content.ReadFromJsonAsync<EntraAntiforgeryInfo>();
        client.DefaultRequestHeaders.Remove("X-Fuse-Entra");
        client.DefaultRequestHeaders.Add("X-Fuse-Entra", token!.RequestToken);
    }

    [Fact]
    public async Task UnknownUserReturnsNotFoundForRevocationAndDisabling()
    {
        var id = Guid.NewGuid();
        var revokeError = await Assert.ThrowsAnyAsync<Generated.ApiException>(() => _adminApi.ApiEntraUsersRevokeAsync(id));
        var disableError = await Assert.ThrowsAnyAsync<Generated.ApiException>(() => _adminApi.ApiEntraUsersDisabledAsync(id, new Generated.SetUserDisabled { Disabled = true }));
        Assert.Equal(404, revokeError.StatusCode);
        Assert.Equal(404, disableError.StatusCode);
    }

    [Fact]
    public async Task SavingSettingsInvalidatesEntraSessionsAndRetainsLocalRecoveryAccess()
    {
        var session = await SignInMappedUser();
        using var client = fixture.CreateAuthenticatedHttpClient(session.Token);
        var command = SettingsCommand();
        command.ClientSecret = null;
        await _adminApi.ApiEntraSettingsPutAsync(command);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tag")).StatusCode);
        Assert.True((await _adminApi.ApiEntraSettingsGetAsync()).HasClientSecret);
        Assert.Equal(HttpStatusCode.OK, (await _adminClient.GetAsync("/api/tag")).StatusCode);
    }

    private Generated.UpdateEntraSettings SettingsCommand()
    {
        return new Generated.UpdateEntraSettings
        {
            Enabled = true,
            TenantId = _tenant,
            ClientId = _tenant,
            PublicOrigin = "http://localhost:5173",
            SessionMinutes = 60,
            ClientSecret = "test-only-secret"
        };
    }

    private async Task<Session> SignInMappedUser(bool linkLocalAccount = false)
    {
        var user = linkLocalAccount
            ? (await _users.CreateUser("linked-user", "test-password", false, [])).Value
            : null;
        var identity = new VerifiedEntraIdentity(_tenant, Guid.NewGuid(), "Test user", [_group], DateTime.UtcNow.AddHours(1));
        var version = (await _store.GetAsync()).SecurityContext.Entra.Version;
        // Exercise the service boundary after OIDC validation. Real Microsoft sign-in is tested separately.
        var result = await _identities.SignIn(identity, version, user?.Id, user?.SessionVersion);
        Assert.True(result.IsSuccess, result.Error);
        return result.Value!;
    }

    private async Task<HttpClient> CreateApiKeyClient(Guid userId)
    {
        var apiKeys = _scope.ServiceProvider.GetRequiredService<IFuseAPIKeyService>();
        var key = (await apiKeys.GenerateNewAPIKey("Test key", userId, [_role.Id])).Value;
        var client = fixture.CreateUnauthenticatedHttpClient();
        client.DefaultRequestHeaders.Add("x-api-key", key.RawKey);
        return client;
    }

    private static async Task<JsonElement> ReadState(HttpClient client)
    {
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/security/state"));
        return document.RootElement.Clone();
    }
}
