using System.Net;
using System.Net.Http.Json;
using Fuse.Core.Areas.PostgreSqlIntegration;
using Fuse.Tests.ApiClient;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.AuthMiddleware;

[Collection("ApiAuthCollection")]
public class PostgreSqlAuthorizationTests(ApiIntegrationFixture fixture) : IAsyncLifetime
{
    private FuseApiClient _admin = null!;

    public async Task InitializeAsync()
    {
        using var anonymous = fixture.CreateUnauthenticatedHttpClient();
        var client = fixture.CreateUnauthenticatedClient();
        if ((await AuthTestHelpers.GetSecurityStateAsync(anonymous)).RequiresSetup)
            await client.ApiSecurityAccountsPostAsync(new CreateSecurityUser
            {
                UserName = "initialAdmin", Password = "InitialPassword123!", IsAdmin = true, RoleIds = []
            });
        _admin = fixture.CreateAuthenticatedClient(await AuthTestHelpers.LoginAsync(client, "initialAdmin", "InitialPassword123!"));
        await _admin.ApiSecuritySettingsAsync(new UpdateSecuritySettings { Posture = SecurityPosture.FullyRestricted });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> UserWith(params string[] permissions)
    {
        var name = $"postgresql-permissions-{Guid.NewGuid():N}";
        var role = await AuthTestHelpers.CreateRoleAsync(_admin, name, permissions);
        await AuthTestHelpers.CreateUserAsync(_admin, name, "PostgreSqlTest123!", customRoleId: role);
        var token = await AuthTestHelpers.LoginAsync(fixture.CreateUnauthenticatedClient(), name, "PostgreSqlTest123!");
        return fixture.CreateAuthenticatedHttpClient(token);
    }

    [Fact]
    public async Task ReaderCanViewDiscovery_ButCannotProbeOrChangeConnections()
    {
        using var reader = await UserWith(PostgreSqlIntegrationPermissions.ReadKey);
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/PostgreSqlIntegration")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/PostgreSqlIntegration/{id}/discovery")).StatusCode);
        foreach (var path in new[] { "/api/PostgreSqlIntegration/validate", $"/api/PostgreSqlIntegration/{id}/validate", "/api/PostgreSqlIntegration" })
            Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(path, new { endpoint = "https://invalid.example" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync($"/api/PostgreSqlIntegration/{id}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.DeleteAsync($"/api/PostgreSqlIntegration/{id}")).StatusCode);

        using var denied = await UserWith();
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync("/api/PostgreSqlIntegration")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync($"/api/PostgreSqlIntegration/{id}/discovery")).StatusCode);
    }

    [Fact]
    public async Task ValidationRequiresItsOwnPermission()
    {
        using var validator = await UserWith(PostgreSqlIntegrationPermissions.ValidateKey);
        // Invalid scheme is rejected before any network connection is attempted.
        Assert.Equal(HttpStatusCode.BadRequest, (await validator.PostAsJsonAsync("/api/PostgreSqlIntegration/validate",
            new { endpoint = "https://invalid.example" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await validator.PostAsJsonAsync($"/api/PostgreSqlIntegration/{Guid.NewGuid()}/validate", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await validator.GetAsync("/api/PostgreSqlIntegration")).StatusCode);
    }

    [Fact]
    public async Task RestrictedEditingAllowsAnonymousReading_ButBlocksConnectionProbes()
    {
        await _admin.ApiSecuritySettingsAsync(new UpdateSecuritySettings { Posture = SecurityPosture.RestrictedEditing });
        try
        {
            using var anonymous = fixture.CreateUnauthenticatedHttpClient();
            Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/PostgreSqlIntegration")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/PostgreSqlIntegration/{Guid.NewGuid()}/discovery")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/PostgreSqlIntegration/validate",
                new { endpoint = "https://invalid.example" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/PostgreSqlIntegration/{Guid.NewGuid()}/validate", new { })).StatusCode);
        }
        finally
        {
            await _admin.ApiSecuritySettingsAsync(new UpdateSecuritySettings { Posture = SecurityPosture.FullyRestricted });
        }
    }

    [Theory]
    [InlineData(SecurityPosture.Unrestricted)]
    [InlineData(SecurityPosture.RestrictedEditing)]
    [InlineData(SecurityPosture.FullyRestricted)]
    public async Task DirectGrantRepairRequiresAnAuthenticatedExplicitWriter(SecurityPosture posture)
    {
        var reader = await UserWith(PostgreSqlIntegrationPermissions.ReadKey, "integrationreviews:read", "accounts:read");
        using var anonymous = fixture.CreateUnauthenticatedHttpClient();
        await _admin.ApiSecuritySettingsAsync(new UpdateSecuritySettings { Posture = posture });
        try
        {
            foreach (var operation in new[] { "preview", "apply" })
            {
                var path = $"/api/PostgreSqlIntegration/{Guid.NewGuid()}/grant-repair/{operation}";
                var body = new { reviewId = Guid.NewGuid(), token = "invalid" };
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path, body)).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(path, body)).StatusCode);
            }
        }
        finally
        {
            reader.Dispose();
            await _admin.ApiSecuritySettingsAsync(new UpdateSecuritySettings { Posture = SecurityPosture.FullyRestricted });
        }
    }
}
