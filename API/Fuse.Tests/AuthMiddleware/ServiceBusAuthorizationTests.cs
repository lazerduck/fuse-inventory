using System.Net;
using System.Net.Http.Json;
using Fuse.Core.Areas.ServiceBusIntegration;
using Fuse.Tests.ApiClient;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.AuthMiddleware;

[Collection("ApiAuthCollection")]
public class ServiceBusAuthorizationTests(ApiIntegrationFixture fixture) : IAsyncLifetime
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
        var name = $"serviceBus-permissions-{Guid.NewGuid():N}";
        var role = await AuthTestHelpers.CreateRoleAsync(_admin, name, permissions);
        await AuthTestHelpers.CreateUserAsync(_admin, name, "ServiceBusTest123!", customRoleId: role);
        var token = await AuthTestHelpers.LoginAsync(fixture.CreateUnauthenticatedClient(), name, "ServiceBusTest123!");
        return fixture.CreateAuthenticatedHttpClient(token);
    }

    [Fact]
    public async Task ReaderCanViewDiscovery_ButCannotProbeOrChangeConnections()
    {
        using var reader = await UserWith(ServiceBusIntegrationPermissions.ReadKey);
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/ServiceBusIntegration")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/ServiceBusIntegration/{id}/discovery")).StatusCode);
        foreach (var path in new[] { "/api/ServiceBusIntegration/validate", $"/api/ServiceBusIntegration/{id}/validate", "/api/ServiceBusIntegration" })
            Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(path, new { endpoint = "https://invalid.example" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync($"/api/ServiceBusIntegration/{id}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.DeleteAsync($"/api/ServiceBusIntegration/{id}")).StatusCode);

        using var denied = await UserWith();
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync("/api/ServiceBusIntegration")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync($"/api/ServiceBusIntegration/{id}/discovery")).StatusCode);
    }

    [Fact]
    public async Task ValidationRequiresItsOwnPermission()
    {
        using var validator = await UserWith(ServiceBusIntegrationPermissions.ValidateKey);
        // Invalid scheme is rejected before any network connection is attempted.
        Assert.Equal(HttpStatusCode.BadRequest, (await validator.PostAsJsonAsync("/api/ServiceBusIntegration/validate",
            new { endpoint = "https://invalid.example" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await validator.PostAsJsonAsync($"/api/ServiceBusIntegration/{Guid.NewGuid()}/validate", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await validator.GetAsync("/api/ServiceBusIntegration")).StatusCode);
    }

    [Fact]
    public async Task RestrictedEditingAllowsAnonymousReading_ButBlocksConnectionProbes()
    {
        await _admin.ApiSecuritySettingsAsync(new UpdateSecuritySettings { Posture = SecurityPosture.RestrictedEditing });
        try
        {
            using var anonymous = fixture.CreateUnauthenticatedHttpClient();
            Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/ServiceBusIntegration")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/ServiceBusIntegration/{Guid.NewGuid()}/discovery")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/ServiceBusIntegration/validate",
                new { endpoint = "https://invalid.example" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/ServiceBusIntegration/{Guid.NewGuid()}/validate", new { })).StatusCode);
        }
        finally
        {
            await _admin.ApiSecuritySettingsAsync(new UpdateSecuritySettings { Posture = SecurityPosture.FullyRestricted });
        }
    }
}
