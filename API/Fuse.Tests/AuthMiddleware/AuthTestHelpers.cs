using Fuse.Core.Models;
using Fuse.Tests.ApiClient;
using Xunit;
using Xunit.Sdk;

namespace Fuse.Tests.AuthMiddleware;

/// <summary>
/// Helper methods for authentication and authorization testing using the generated API client.
/// </summary>
public static class AuthTestHelpers
{
    /// <summary>
    /// Login with the given credentials and return the session token.
    /// </summary>
    public static async Task<string> LoginAsync(FuseApiClient client, string username, string password)
    {
        try
        {
            var session = await client.ApiSecurityLoginAsync(new LoginSecurityUser
            {
                UserName = username,
                Password = password
            });

            Assert.NotNull(session?.Token);
            return session.Token;
        }
        catch (ApiException ex)
        {
            throw new XunitException($"Login failed with status {ex.StatusCode}: {ex.Response}");
        }
    }

    /// <summary>
    /// Create a user with the given role and optional custom role assignment.
    /// </summary>
    public static async Task<Guid> CreateUserAsync(
        FuseApiClient adminClient,
        string username,
        string password,
        Core.Models.SecurityRole role = Core.Models.SecurityRole.Reader,
        Guid? customRoleId = null)
    {
        try
        {
            // Convert Core.Models.SecurityRole to ApiClient.SecurityRole
            var apiRole = role == Core.Models.SecurityRole.Admin 
                ? ApiClient.SecurityRole.Admin 
                : ApiClient.SecurityRole.Reader;

            var user = await adminClient.ApiSecurityAccountsPostAsync(new CreateSecurityUser
            {
                UserName = username,
                Password = password,
                Role = apiRole
            });

            Assert.NotNull(user);

            // If custom role specified, assign it
            if (customRoleId.HasValue && customRoleId != Guid.Empty)
            {
                await adminClient.ApiSecurityAccountsRolesAsync(user.Id, new AssignRolesToUser
                {
                    UserId = user.Id,
                    RoleIds = new[] { customRoleId.Value }
                });
            }

            return user.Id;
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            // User already exists - fetch their ID
            var users = await adminClient.ApiSecurityAccountsGetAsync();
            var existingUser = users.FirstOrDefault(u => u.UserName == username);
            if (existingUser != null)
            {
                return existingUser.Id;
            }
            throw new XunitException($"User '{username}' reported as existing but could not be found");
        }
        catch (ApiException ex)
        {
            throw new XunitException($"Failed to create user '{username}' with status {ex.StatusCode}: {ex.Response}");
        }
    }

    /// <summary>
    /// Create a custom role with the specified permissions.
    /// </summary>
    public static async Task<Guid> CreateRoleAsync(
        FuseApiClient adminClient,
        string roleName,
        IReadOnlyList<Core.Models.Permission> permissions)
    {
        try
        {
            // Convert Core.Models.Permission to ApiClient.Permission
            var apiPermissions = permissions.Select(p => 
                (ApiClient.Permission)Enum.Parse(typeof(ApiClient.Permission), p.ToString())
            ).ToList();

            var role = await adminClient.ApiRolePostAsync(new CreateRole
            {
                Name = roleName,
                Description = $"Test role: {roleName}",
                Permissions = apiPermissions
            });

            Assert.NotNull(role);
            return role.Id;
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            // Role already exists - fetch its ID
            var roles = await adminClient.ApiRoleGetAsync();
            var existingRole = roles.FirstOrDefault(r => r.Name == roleName);
            if (existingRole != null)
            {
                return existingRole.Id;
            }
            throw new XunitException($"Role '{roleName}' reported as existing but could not be found");
        }
        catch (ApiException ex)
        {
            throw new XunitException($"Failed to create role '{roleName}' with status {ex.StatusCode}: {ex.Response}");
        }
    }

    /// <summary>
    /// Setup a test user scenario: create user, optionally create custom role, login, and return token.
    /// </summary>
    public static async Task<string> SetupUserScenarioAsync(
        FuseApiClient adminClient,
        TestUserScenario scenario,
        Func<FuseApiClient> createUnauthenticatedClient)
    {
        // Create custom role if needed
        Guid? customRoleId = null;
        if (scenario.SpecificPermissions != null)
        {
            customRoleId = await CreateRoleAsync(
                adminClient,
                $"role-{scenario.ScenarioId}",
                scenario.SpecificPermissions);
        }

        // Create the user
        await CreateUserAsync(
            adminClient,
            scenario.UserName,
            scenario.Password,
            scenario.Role,
            customRoleId);

        // Login as the new user to get token
        var unauthClient = createUnauthenticatedClient();
        var token = await LoginAsync(unauthClient, scenario.UserName, scenario.Password);

        return token;
    }

    /// <summary>
    /// Get security state (used to check if setup is required).
    /// Uses raw HTTP because the generated client has serialization issues with nullable CurrentUser.
    /// </summary>
    public static async Task<SecurityStateResponse> GetSecurityStateAsync(HttpClient httpClient)
    {
        try
        {
            var response = await httpClient.GetAsync("/api/security/state");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            // Use case-insensitive JSON parsing
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var state = System.Text.Json.JsonSerializer.Deserialize<SecurityStateDto>(json, options);
            
            if (state == null)
                throw new XunitException("Security state response was null");
            
            return new SecurityStateResponse
            {
                Level = Enum.Parse<ApiClient.SecurityLevel>(state.Level!),
                UpdatedAt = state.UpdatedAt,
                RequiresSetup = state.RequiresSetup,
                HasUsers = state.HasUsers,
                CurrentUser = null // We don't need this for test setup
            };
        }
        catch (Exception ex)
        {
            throw new XunitException($"Failed to get security state: {ex.GetType().Name} - {ex.Message}");
        }
    }
    
    // DTO for deserializing security state response
    private class SecurityStateDto
    {
        public string? Level { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public bool RequiresSetup { get; set; }
        public bool HasUsers { get; set; }
    }

    /// <summary>
    /// Execute a request and verify the response has the expected status code.
    /// Uses raw HttpClient for generic endpoint testing (permission matrix).
    /// </summary>
    public static async Task<HttpResponseMessage> ExecuteEndpointTestAsync(
        HttpClient client,
        EndpointAccessTest test)
    {
        var request = new HttpRequestMessage(new HttpMethod(test.HttpMethod), test.Endpoint);

        if (test.RequestBody != null)
        {
            request.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(test.RequestBody),
                System.Text.Encoding.UTF8,
                "application/json");
        }

        var response = await client.SendAsync(request);
        return response;
    }
}
