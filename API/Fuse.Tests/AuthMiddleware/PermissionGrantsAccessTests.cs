using System.Net;
using System.Net.Http.Json;
using Fuse.Tests.ApiClient;
using Fuse.Tests.TestInfrastructure;
using Xunit;
using Xunit.Sdk;

namespace Fuse.Tests.AuthMiddleware;

/// <summary>
/// Focused tests that prove: a non-admin user whose role includes a specific permission
/// can still reach the endpoint guarded by that permission — and is blocked (403) without it.
///
/// These tests complement the broad permission matrix in MiddlewareAuthorizationTests.
/// Each test case exercises exactly one permission against exactly one endpoint so the
/// relationship is unambiguous.
/// </summary>
[Collection("ApiAuthCollection")]
[Trait("Category", "AuthMiddleware")]
public class PermissionGrantsAccessTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _apiFixture;
    private FuseApiClient? _adminClient;

    // Token for a user that holds exactly the permission named by the dictionary key
    private readonly Dictionary<string, string> _permittedTokens = new();

    // Token for a user that belongs to an empty custom role (no permissions at all)
    private string? _noPermissionsToken;

    public PermissionGrantsAccessTests(ApiIntegrationFixture apiFixture)
    {
        _apiFixture = apiFixture;
    }

    // ---------------------------------------------------------------------------
    // Test data: permission key → (HTTP method, endpoint path)
    // Each row asserts that holding one named permission unlocks exactly one endpoint.
    // ---------------------------------------------------------------------------
    public static IEnumerable<object[]> ReadPermissionEndpointCases => new object[][]
    {
        // Permission-specifically gated (handled outside the CRUD matrix)
        new object[] { nameof(Permission.AuditLogsView),        "GET", "/api/audit" },
        new object[] { nameof(Permission.ConfigurationExport),  "GET", "/api/config/export" },
        new object[] { nameof(Permission.UsersRead),            "GET", "/api/security/accounts" },
        new object[] { nameof(Permission.RolesRead),            "GET", "/api/roles" },

        // Standard CRUD read permissions (handled by the CRUD permission matrix)
        new object[] { nameof(Permission.AccountsRead),         "GET", "/api/account" },
        new object[] { nameof(Permission.ApplicationsRead),     "GET", "/api/application" },
        new object[] { nameof(Permission.IdentitiesRead),       "GET", "/api/identity" },
        new object[] { nameof(Permission.DataStoresRead),       "GET", "/api/datastore" },
        new object[] { nameof(Permission.PlatformsRead),        "GET", "/api/platform" },
        new object[] { nameof(Permission.EnvironmentsRead),     "GET", "/api/environment" },
        new object[] { nameof(Permission.ExternalResourcesRead),"GET", "/api/externalresource" },
        new object[] { nameof(Permission.PositionsRead),        "GET", "/api/position" },
        new object[] { nameof(Permission.RisksRead),            "GET", "/api/risk" },
    };

    public static IEnumerable<object[]> WritePermissionEndpointCases => new object[][]
    {
        new object[]
        {
            nameof(Permission.AccountsCreate),
            "POST", "/api/account",
            new { name = "PermTestAccount", description = "Created by permission grant test" }
        },
        new object[]
        {
            nameof(Permission.ApplicationsCreate),
            "POST", "/api/application",
            new { name = "PermTestApp", description = "Created by permission grant test" }
        },
        new object[]
        {
            nameof(Permission.IdentitiesCreate),
            "POST", "/api/identity",
            new { name = "PermTestIdentity", description = "Created by permission grant test" }
        },
        new object[]
        {
            nameof(Permission.RisksCreate),
            "POST", "/api/risk",
            new { title = "PermTestRisk", description = "Created by permission grant test" }
        },
        new object[]
        {
            nameof(Permission.UsersCreate),
            "POST", "/api/security/accounts",
            new { userName = "perm_newuser_test", password = "NewUserPermTest123!", role = "Reader" }
        },
        new object[]
        {
            nameof(Permission.RolesCreate),
            "POST", "/api/roles",
            new { name = "PermTestRole", description = "Test", permissions = new string[] { } }
        },
    };

    // ---------------------------------------------------------------------------
    // Test 1: Non-admin user WITH the required permission CAN reach the endpoint
    // ---------------------------------------------------------------------------

    /// <summary>
    /// A non-admin user whose role contains only the permission listed gets a non-403/401
    /// response — proving that permission-based access works independently of admin status.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadPermissionEndpointCases))]
    public async Task NonAdminWithPermission_CanRead_GatedEndpoint(
        string permKey, string method, string endpoint)
    {
        if (!_permittedTokens.TryGetValue(permKey, out var token) || string.IsNullOrEmpty(token))
            throw new XunitException($"Test setup failed: could not obtain token for permission '{permKey}'");

        var client = _apiFixture.CreateAuthenticatedHttpClient(token);
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), endpoint));
        var status = (int)response.StatusCode;

        // Authorization passed when we do NOT get 401 or 403.
        // 200 (ok/empty list) and 404 (no data) are both fine.
        Assert.True(
            status != 401 && status != 403,
            $"Non-admin user with only '{permKey}' should be allowed to {method} {endpoint}, but got {status}");
    }

    /// <summary>
    /// A non-admin user whose role contains the write permission gets through the auth check
    /// for the corresponding mutation endpoint (the response may be 201/400/409 — any of those
    /// means authorization passed; only 403/401 indicate a failure).
    /// </summary>
    [Theory]
    [MemberData(nameof(WritePermissionEndpointCases))]
    public async Task NonAdminWithPermission_CanWrite_GatedEndpoint(
        string permKey, string method, string endpoint, object requestBody)
    {
        if (!_permittedTokens.TryGetValue(permKey, out var token) || string.IsNullOrEmpty(token))
            throw new XunitException($"Test setup failed: could not obtain token for permission '{permKey}'");

        var client = _apiFixture.CreateAuthenticatedHttpClient(token);
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint)
        {
            Content = JsonContent.Create(requestBody)
        };
        var response = await client.SendAsync(request);
        var status = (int)response.StatusCode;

        // Auth check passed when we do NOT get 401 or 403.
        // 201 (created), 400/409 (validation / conflict with existing data) are all acceptable.
        Assert.True(
            status != 401 && status != 403,
            $"Non-admin user with only '{permKey}' should pass the auth check for {method} {endpoint}, but got {status}");
    }

    // ---------------------------------------------------------------------------
    // Test 2: Non-admin user WITHOUT any permission is BLOCKED from every gated endpoint
    // ---------------------------------------------------------------------------

    /// <summary>
    /// A non-admin user with an empty role (no permissions) receives 403 Forbidden
    /// for every read endpoint that requires a specific permission.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadPermissionEndpointCases))]
    public async Task NonAdminWithNoPermissions_IsForbidden_FromReadEndpoint(
        string permKey, string method, string endpoint)
    {
        _ = permKey; // the endpoint is what matters here
        if (string.IsNullOrEmpty(_noPermissionsToken))
            throw new XunitException("Test setup failed: no-permissions user token is missing");

        var client = _apiFixture.CreateAuthenticatedHttpClient(_noPermissionsToken);
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), endpoint));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// A non-admin user with an empty role (no permissions) receives 403 Forbidden
    /// for every write endpoint that requires a specific permission.
    /// </summary>
    [Theory]
    [MemberData(nameof(WritePermissionEndpointCases))]
    public async Task NonAdminWithNoPermissions_IsForbidden_FromWriteEndpoint(
        string permKey, string method, string endpoint, object requestBody)
    {
        _ = permKey;
        if (string.IsNullOrEmpty(_noPermissionsToken))
            throw new XunitException("Test setup failed: no-permissions user token is missing");

        var client = _apiFixture.CreateAuthenticatedHttpClient(_noPermissionsToken);
        var request = new HttpRequestMessage(new HttpMethod(method), endpoint)
        {
            Content = JsonContent.Create(requestBody)
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        var httpClient = _apiFixture.CreateUnauthenticatedHttpClient();
        var unauthClient = _apiFixture.CreateUnauthenticatedClient();
        var state = await AuthTestHelpers.GetSecurityStateAsync(httpClient);

        if (state.RequiresSetup)
        {
            try
            {
                await unauthClient.ApiSecurityAccountsPostAsync(new CreateSecurityUser
                {
                    UserName = "initialAdmin",
                    Password = "InitialPassword123!",
                    Role = SecurityRole.Admin
                });
            }
            catch (ApiException ex) when (ex.StatusCode == 409) { /* Already exists */ }
        }

        var adminToken = await AuthTestHelpers.LoginAsync(unauthClient, "initialAdmin", "InitialPassword123!");
        _adminClient = _apiFixture.CreateAuthenticatedClient(adminToken);

        // Ensure the site is fully restricted so all permission gates are active
        try
        {
            await _adminClient.ApiSecuritySettingsAsync(new UpdateSecuritySettings
            {
                Level = SecurityLevel.FullyRestricted
            });
        }
        catch { /* May already be set */ }

        // Create a user with NO permissions (assigned to an empty custom role)
        _noPermissionsToken = await SetupPermissionUserAsync("pgtst_noperm", Array.Empty<Permission>());

        // Create one user per permission we want to verify
        var allCases = ReadPermissionEndpointCases.Concat(WritePermissionEndpointCases)
            .Select(row => (string)row[0])
            .Distinct();

        foreach (var permKey in allCases)
        {
            if (!Enum.TryParse<Permission>(permKey, out var permission))
                continue;

            _permittedTokens[permKey] = await SetupPermissionUserAsync(
                $"pgtst_{permKey.ToLowerInvariant()}",
                new[] { permission });
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a role with the given permissions, creates a user assigned only to that role,
    /// and returns a login token. If permissions is empty the user gets an empty custom role.
    /// </summary>
    private async Task<string> SetupPermissionUserAsync(string username, IReadOnlyList<Permission> permissions)
    {
        // Create or retrieve the role
        Guid? roleId = null;
        var roleName = $"role-{username}";

        var apiPermissions = permissions
            .Select(p => (ApiClient.Permission)Enum.Parse(typeof(ApiClient.Permission), p.ToString()))
            .ToList();

        try
        {
            var role = await _adminClient!.ApiRolePostAsync(new CreateRole
            {
                Name = roleName,
                Description = $"Single-permission test role for {username}",
                Permissions = apiPermissions
            });
            roleId = role.Id;
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            var roles = await _adminClient!.ApiRoleGetAsync();
            roleId = roles.FirstOrDefault(r => r.Name == roleName)?.Id;
        }

        // Create or retrieve the user
        Guid userId;
        try
        {
            var user = await _adminClient!.ApiSecurityAccountsPostAsync(new CreateSecurityUser
            {
                UserName = username,
                Password = "PermTest123!",
                Role = SecurityRole.Reader
            });
            userId = user.Id;
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            var users = await _adminClient!.ApiSecurityAccountsGetAsync();
            var existing = users.FirstOrDefault(u => u.UserName == username)
                ?? throw new XunitException($"User '{username}' reported as existing but could not be found");
            userId = existing.Id;
        }

        // Assign exactly the custom role (replacing any built-in role so the user truly has
        // only the permissions in this role)
        var roleIds = roleId.HasValue
            ? new[] { roleId.Value }
            : Array.Empty<Guid>();

        await _adminClient!.ApiSecurityAccountsRolesAsync(userId, new AssignRolesToUser
        {
            UserId = userId,
            RoleIds = roleIds
        });

        return await AuthTestHelpers.LoginAsync(_apiFixture.CreateUnauthenticatedClient(), username, "PermTest123!");
    }
}
