using System.Net;
using Fuse.Tests.ApiClient;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.AuthMiddleware;

/// <summary>
/// Tests to verify that site-wide security level settings override user-specific permissions.
/// These tests ensure that when SecurityLevel.None or SecurityLevel.RestrictedEditing is set,
/// the site-wide rules allow access beyond individual user account settings.
/// </summary>
[Collection("ApiAuthCollection")]
[Trait("Category", "AuthMiddleware")]
public class SecurityLevelOverrideTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _apiFixture;
    private FuseApiClient? _adminClient;
    private string? _adminToken;

    public SecurityLevelOverrideTests(ApiIntegrationFixture apiFixture)
    {
        _apiFixture = apiFixture;
    }

    public async Task InitializeAsync()
    {
        var unauthClient = _apiFixture.CreateUnauthenticatedClient();
        var httpClient = _apiFixture.CreateUnauthenticatedHttpClient();
        var state = await AuthTestHelpers.GetSecurityStateAsync(httpClient);

        if (state.RequiresSetup)
        {
            await unauthClient.ApiSecurityAccountsPostAsync(new CreateSecurityUser
            {
                UserName = "initialAdmin",
                Password = "InitialPassword123!",
                Role = ApiClient.SecurityRole.Admin
            });
            _adminToken = await AuthTestHelpers.LoginAsync(unauthClient, "initialAdmin", "InitialPassword123!");
        }
        else
        {
            _adminToken = await AuthTestHelpers.LoginAsync(unauthClient, "initialAdmin", "InitialPassword123!");
        }

        _adminClient = _apiFixture.CreateAuthenticatedClient(_adminToken);
    }

    public async Task DisposeAsync()
    {
        // Restore security level to FullyRestricted for other tests
        if (_adminClient is not null)
        {
            try
            {
                await _adminClient.ApiSecuritySettingsAsync(new UpdateSecuritySettings
                {
                    Level = ApiClient.SecurityLevel.FullyRestricted
                });
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }

    [Fact]
    public async Task SecurityLevel_None_Allows_Unauthenticated_Read_Access()
    {
        // Arrange: Set security level to None (public access)
        await _adminClient!.ApiSecuritySettingsAsync(new UpdateSecuritySettings
        {
            Level = ApiClient.SecurityLevel.None
        });

        // Act: Try to read applications without authentication
        var unauthClient = _apiFixture.CreateUnauthenticatedClient();
        
        // Assert: Should succeed with 200 OK
        var response = await unauthClient.ApiApplicationGetAsync();
        Assert.NotNull(response);
    }

    [Fact]
    public async Task SecurityLevel_None_Allows_Unauthenticated_Write_Access()
    {
        // Arrange: Set security level to None (public access)
        await _adminClient!.ApiSecuritySettingsAsync(new UpdateSecuritySettings
        {
            Level = ApiClient.SecurityLevel.None
        });

        // Act: Try to create an application without authentication
        var unauthClient = _apiFixture.CreateUnauthenticatedClient();
        var createRequest = new CreateApplication
        {
            Name = "Test App",
            Description = "Test Description"
        };

        // Assert: Should succeed with 201 Created
        var response = await unauthClient.ApiApplicationPostAsync(createRequest);
        Assert.NotNull(response);
        Assert.Equal("Test App", response.Name);
    }

    [Fact]
    public async Task SecurityLevel_RestrictedEditing_Allows_Unauthenticated_Read_Access()
    {
        // Arrange: Set security level to RestrictedEditing (public read, restricted write)
        await _adminClient!.ApiSecuritySettingsAsync(new UpdateSecuritySettings
        {
            Level = ApiClient.SecurityLevel.RestrictedEditing
        });

        // Act: Try to read applications without authentication
        var unauthClient = _apiFixture.CreateUnauthenticatedClient();
        
        // Assert: Should succeed with 200 OK
        var response = await unauthClient.ApiApplicationGetAsync();
        Assert.NotNull(response);
    }

    [Fact]
    public async Task SecurityLevel_RestrictedEditing_Blocks_Unauthenticated_Write_Access()
    {
        // Arrange: Set security level to RestrictedEditing (public read, restricted write)
        await _adminClient!.ApiSecuritySettingsAsync(new UpdateSecuritySettings
        {
            Level = ApiClient.SecurityLevel.RestrictedEditing
        });

        // Act & Assert: Try to create an application without authentication
        var unauthClient = _apiFixture.CreateUnauthenticatedClient();
        var createRequest = new CreateApplication
        {
            Name = "Test App",
            Description = "Test Description"
        };

        var exception = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await unauthClient.ApiApplicationPostAsync(createRequest);
        });

        // Should get 401 Unauthorized (not 403) since no user is authenticated
        Assert.Equal((int)HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task SecurityLevel_FullyRestricted_Blocks_Unauthenticated_Read_Access()
    {
        // Arrange: Set security level to FullyRestricted (all access requires authentication)
        await _adminClient!.ApiSecuritySettingsAsync(new UpdateSecuritySettings
        {
            Level = ApiClient.SecurityLevel.FullyRestricted
        });

        // Act & Assert: Try to read applications without authentication
        var unauthClient = _apiFixture.CreateUnauthenticatedClient();
        
        var exception = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await unauthClient.ApiApplicationGetAsync();
        });

        Assert.Equal((int)HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task SecurityLevel_FullyRestricted_Blocks_Unauthenticated_Write_Access()
    {
        // Arrange: Set security level to FullyRestricted (all access requires authentication)
        await _adminClient!.ApiSecuritySettingsAsync(new UpdateSecuritySettings
        {
            Level = ApiClient.SecurityLevel.FullyRestricted
        });

        // Act & Assert: Try to create an application without authentication
        var unauthClient = _apiFixture.CreateUnauthenticatedClient();
        var createRequest = new CreateApplication
        {
            Name = "Test App",
            Description = "Test Description"
        };

        var exception = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await unauthClient.ApiApplicationPostAsync(createRequest);
        });

        Assert.Equal((int)HttpStatusCode.Unauthorized, exception.StatusCode);
    }
}
