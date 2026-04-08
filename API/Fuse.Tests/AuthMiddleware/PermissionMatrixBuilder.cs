using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.Audit;
using Fuse.Core.Areas.Config;
using Fuse.Core.Areas.DataStore;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.ExternalResource;
using Fuse.Core.Areas.Identity;
using Fuse.Core.Areas.KumaIntegration;
using Fuse.Core.Areas.MessageBroker;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.Position;
using Fuse.Core.Areas.Responsibility;
using Fuse.Core.Areas.Risk;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Areas.Security.Permissions;
using Fuse.Core.Areas.SqlIntegration;

namespace Fuse.Tests.AuthMiddleware;

/// <summary>
/// Defines a test scenario with a specific user role/permissions and expected endpoint access.
/// </summary>
public record TestUserScenario
{
    /// <summary>
    /// Unique identifier for the scenario (e.g., "admin-user", "reader-user").
    /// </summary>
    public required string ScenarioId { get; init; }

    /// <summary>
    /// Username to create for this scenario.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Password for the user.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// Whether this user is an admin (bypasses all permission checks).
    /// </summary>
    public required bool IsAdmin { get; init; }

    /// <summary>
    /// Specific permission keys for custom roles (optional).
    /// If null, the user has no custom role and relies on admin status only.
    /// </summary>
    public IReadOnlyList<string>? SpecificPermissions { get; init; }

    /// <summary>
    /// Display name for test output.
    /// </summary>
    public required string DisplayName { get; init; }

    public override string ToString() => DisplayName;
}

/// <summary>
/// Defines an endpoint test case: which HTTP operation on which endpoint,
/// and what response status code is expected for a given user scenario.
/// </summary>
public record EndpointAccessTest
{
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, PATCH).
    /// </summary>
    public required string HttpMethod { get; init; }

    /// <summary>
    /// API endpoint path (e.g., "/api/account", "/api/audit").
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// Optional request body for POST/PUT/PATCH operations.
    /// </summary>
    public object? RequestBody { get; init; }

    /// <summary>
    /// Expected HTTP status code (200, 201, 204, 401, 403, etc.).
    /// </summary>
    public required int ExpectedStatusCode { get; init; }

    /// <summary>
    /// Display name for test output.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Required permission key to access this endpoint (e.g., "application:read").
    /// </summary>
    public string? RequiredPermissionKey { get; init; }

    public override string ToString() => $"{HttpMethod} {Endpoint}";
}

/// <summary>
/// Contains test scenarios and endpoint access expectations for middleware authorization testing.
/// </summary>
public static class PermissionMatrixBuilder
{
    /// <summary>
    /// Admin user with full access to all endpoints.
    /// </summary>
    public static readonly TestUserScenario AdminUser = new()
    {
        ScenarioId = "admin-user",
        UserName = "admin_test",
        Password = "AdminPassword123!",
        IsAdmin = true,
        DisplayName = "Admin User"
    };

    /// <summary>
    /// Reader user with no special permissions (non-admin, no custom role).
    /// In FullyRestricted mode this user is blocked from all permission-gated endpoints.
    /// </summary>
    public static readonly TestUserScenario ReaderUser = new()
    {
        ScenarioId = "reader-user",
        UserName = "reader_test",
        Password = "ReaderPassword123!",
        IsAdmin = false,
        DisplayName = "Reader User (No Permissions)"
    };

    /// <summary>
    /// No permissions user (custom role with no permissions).
    /// </summary>
    public static readonly TestUserScenario NoPermissionsUser = new()
    {
        ScenarioId = "no-permissions-user",
        UserName = "restricted_test",
        Password = "RestrictedPassword123!",
        IsAdmin = false,
        SpecificPermissions = Array.Empty<string>(),
        DisplayName = "Restricted User (Empty Custom Role)"
    };

    /// <summary>
    /// Limited permissions user (can read core entities, but not write).
    /// </summary>
    public static readonly TestUserScenario LimitedPermissionsUser = new()
    {
        ScenarioId = "limited-user",
        UserName = "limited_test",
        Password = "LimitedPassword123!",
        IsAdmin = false,
        SpecificPermissions = new[]
        {
            AccountPermissions.ReadKey,
            ApplicationPermissions.ReadKey,
            IdentityPermissions.ReadKey,
            DataStorePermissions.ReadKey,
            PlatformPermissions.ReadKey,
            EnvironmentPermissions.ReadKey,
            ExternalResourcePermissions.ReadKey,
            PositionPermissions.ReadKey,
            ResponsibilityPermissions.ReadKey,
            RiskPermissions.ReadKey,
            UserAccountPermissions.ReadKey,
            RolePermissions.ReadKey,
            AuditPermissions.ViewKey,
        },
        DisplayName = "Limited User (Read-Only Custom)"
    };

    /// <summary>
    /// Power user with create/read/update permissions but no delete or sensitive actions.
    /// </summary>
    public static readonly TestUserScenario PowerUser = new()
    {
        ScenarioId = "power-user",
        UserName = "power_test",
        Password = "PowerPassword123!",
        IsAdmin = false,
        SpecificPermissions = new[]
        {
            AccountPermissions.ReadKey,
            AccountPermissions.CreateKey,
            AccountPermissions.UpdateKey,
            ApplicationPermissions.ReadKey,
            ApplicationPermissions.CreateKey,
            ApplicationPermissions.UpdateKey,
            IdentityPermissions.ReadKey,
            IdentityPermissions.CreateKey,
            IdentityPermissions.UpdateKey,
            DataStorePermissions.ReadKey,
            DataStorePermissions.CreateKey,
            DataStorePermissions.UpdateKey,
            PlatformPermissions.ReadKey,
            PlatformPermissions.CreateKey,
            PlatformPermissions.UpdateKey,
            EnvironmentPermissions.ReadKey,
            EnvironmentPermissions.CreateKey,
            EnvironmentPermissions.UpdateKey,
            ExternalResourcePermissions.ReadKey,
            ExternalResourcePermissions.CreateKey,
            ExternalResourcePermissions.UpdateKey,
            PositionPermissions.ReadKey,
            PositionPermissions.CreateKey,
            PositionPermissions.UpdateKey,
            ResponsibilityPermissions.ReadKey,
            ResponsibilityPermissions.CreateKey,
            ResponsibilityPermissions.UpdateKey,
            RiskPermissions.ReadKey,
            RiskPermissions.CreateKey,
            RiskPermissions.UpdateKey,
            RiskPermissions.ApproveKey,
            UserAccountPermissions.ReadKey,
            RolePermissions.ReadKey,
            AuditPermissions.ViewKey,
        },
        DisplayName = "Power User (Create/Read/Update)"
    };

    /// <summary>
    /// User with only user management permissions.
    /// </summary>
    public static readonly TestUserScenario UserManagerUser = new()
    {
        ScenarioId = "user-manager",
        UserName = "usermgr_test",
        Password = "UserMgrPassword123!",
        IsAdmin = false,
        SpecificPermissions = new[]
        {
            UserAccountPermissions.ReadKey,
            UserAccountPermissions.CreateKey,
            UserAccountPermissions.UpdateKey,
            UserAccountPermissions.DeleteKey,
        },
        DisplayName = "User Manager (Users Only)"
    };

    /// <summary>
    /// User with only role management permissions.
    /// </summary>
    public static readonly TestUserScenario RoleManagerUser = new()
    {
        ScenarioId = "role-manager",
        UserName = "rolemgr_test",
        Password = "RoleMgrPassword123!",
        IsAdmin = false,
        SpecificPermissions = new[]
        {
            RolePermissions.ReadKey,
            RolePermissions.CreateKey,
            RolePermissions.UpdateKey,
            RolePermissions.DeleteKey,
        },
        DisplayName = "Role Manager (Roles Only)"
    };

    /// <summary>
    /// User with only read permissions for users and roles.
    /// </summary>
    public static readonly TestUserScenario SecurityReaderUser = new()
    {
        ScenarioId = "security-reader",
        UserName = "secreader_test",
        Password = "SecReaderPassword123!",
        IsAdmin = false,
        SpecificPermissions = new[]
        {
            UserAccountPermissions.ReadKey,
            RolePermissions.ReadKey,
        },
        DisplayName = "Security Reader (Read-Only)"
    };

    /// <summary>
    /// User with audit log viewing and configuration export permissions.
    /// </summary>
    public static readonly TestUserScenario AuditorUser = new()
    {
        ScenarioId = "auditor",
        UserName = "auditor_test",
        Password = "AuditorPassword123!",
        IsAdmin = false,
        SpecificPermissions = new[]
        {
            AuditPermissions.ViewKey,
            ConfigPermissions.ExportKey,
        },
        DisplayName = "Auditor (Audit + Config Export)"
    };

    /// <summary>
    /// All test user scenarios for comprehensive permission matrix testing.
    /// </summary>
    public static IReadOnlyList<TestUserScenario> AllScenarios => new[]
    {
        AdminUser,
        ReaderUser,
        LimitedPermissionsUser,
        PowerUser,
        NoPermissionsUser,
        UserManagerUser,
        RoleManagerUser,
        SecurityReaderUser,
        AuditorUser
    };

    /// <summary>
    /// Core endpoint tests covering CRUD operations and sensitive endpoints.
    /// These tests are run against each user scenario to verify authorization.
    /// </summary>
    public static IReadOnlyList<EndpointAccessTest> CoreEndpointTests => new EndpointAccessTest[]
    {
        // Account endpoints
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/account",
            DisplayName = "Get Accounts",
            ExpectedStatusCode = 200,
            RequiredPermissionKey = AccountPermissions.ReadKey
        },
        new()
        {
            HttpMethod = "POST",
            Endpoint = "/api/account",
            DisplayName = "Create Account",
            RequestBody = new { name = "TestAccount", description = "Test" },
            ExpectedStatusCode = 201,
            RequiredPermissionKey = AccountPermissions.CreateKey
        },

        // Application endpoints
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/application",
            DisplayName = "Get Applications",
            ExpectedStatusCode = 200,
            RequiredPermissionKey = ApplicationPermissions.ReadKey
        },
        new()
        {
            HttpMethod = "POST",
            Endpoint = "/api/application",
            DisplayName = "Create Application",
            RequestBody = new { name = "TestApp", description = "Test" },
            ExpectedStatusCode = 201,
            RequiredPermissionKey = ApplicationPermissions.CreateKey
        },

        // Audit logs (sensitive - read-only, but permission-gated)
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/audit",
            DisplayName = "Get Audit Logs",
            ExpectedStatusCode = 200,
            RequiredPermissionKey = AuditPermissions.ViewKey
        },

        // Config export (highly sensitive - requires specific permission)
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/config/export",
            DisplayName = "Export Configuration",
            ExpectedStatusCode = 200,
            RequiredPermissionKey = ConfigPermissions.ExportKey
        },

        // Security endpoints - User management
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/security/accounts",
            DisplayName = "Get Security Accounts",
            ExpectedStatusCode = 200,
            RequiredPermissionKey = UserAccountPermissions.ReadKey
        },
        new()
        {
            HttpMethod = "POST",
            Endpoint = "/api/security/accounts",
            DisplayName = "Create Security Account",
            RequestBody = new { userName = "newuser", password = "NewPassword123!", isAdmin = false, roleIds = new string[] { } },
            ExpectedStatusCode = 201,
            RequiredPermissionKey = UserAccountPermissions.CreateKey
        },

        // Role management endpoints
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/role",
            DisplayName = "Get Roles",
            ExpectedStatusCode = 200,
            RequiredPermissionKey = RolePermissions.ReadKey
        },
        new()
        {
            HttpMethod = "POST",
            Endpoint = "/api/role",
            DisplayName = "Create Role",
            RequestBody = new { name = "NewRole", permissions = new string[] { } },
            ExpectedStatusCode = 201,
            RequiredPermissionKey = RolePermissions.CreateKey
        },

        // Positions endpoints
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/position",
            DisplayName = "Get Positions",
            ExpectedStatusCode = 200,
            RequiredPermissionKey = PositionPermissions.ReadKey
        },

        // Identities endpoints
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/identity",
            DisplayName = "Get Identities",
            ExpectedStatusCode = 200,
            RequiredPermissionKey = IdentityPermissions.ReadKey
        },
    };

    /// <summary>
    /// Get expected status codes for a user scenario and endpoint test.
    /// Applies permission inference logic.
    /// </summary>
    public static int GetExpectedStatusCode(TestUserScenario scenario, EndpointAccessTest test)
    {
        // Admins bypass all permission checks
        if (scenario.IsAdmin)
        {
            return test.ExpectedStatusCode;
        }

        // For endpoints that require a specific permission key, check if user has it
        if (test.RequiredPermissionKey is not null)
        {
            var permissions = scenario.SpecificPermissions;

            // If SpecificPermissions is not null, the user has a custom role
            if (permissions != null)
            {
                if (!permissions.Contains(test.RequiredPermissionKey, StringComparer.OrdinalIgnoreCase))
                {
                    return 403; // Forbidden - missing required permission
                }
                return test.ExpectedStatusCode;
            }

            // Non-admin user with no custom role has no permissions in FullyRestricted mode
            return 403;
        }

        // For endpoints with no required permission key, any authenticated user can access
        return test.ExpectedStatusCode;
    }

    /// <summary>
    /// Get all available permission keys.
    /// </summary>
    public static IReadOnlyList<string> GetAllPermissions() => new[]
    {
        // Applications
        ApplicationPermissions.ReadKey,
        ApplicationPermissions.CreateKey,
        ApplicationPermissions.UpdateKey,
        ApplicationPermissions.DeleteKey,

        // Accounts
        AccountPermissions.ReadKey,
        AccountPermissions.CreateKey,
        AccountPermissions.UpdateKey,
        AccountPermissions.DeleteKey,

        // Identities
        IdentityPermissions.ReadKey,
        IdentityPermissions.CreateKey,
        IdentityPermissions.UpdateKey,
        IdentityPermissions.DeleteKey,

        // DataStores
        DataStorePermissions.ReadKey,
        DataStorePermissions.CreateKey,
        DataStorePermissions.UpdateKey,
        DataStorePermissions.DeleteKey,

        // Platforms
        PlatformPermissions.ReadKey,
        PlatformPermissions.CreateKey,
        PlatformPermissions.UpdateKey,
        PlatformPermissions.DeleteKey,

        // Environments
        EnvironmentPermissions.ReadKey,
        EnvironmentPermissions.CreateKey,
        EnvironmentPermissions.UpdateKey,
        EnvironmentPermissions.DeleteKey,

        // ExternalResources
        ExternalResourcePermissions.ReadKey,
        ExternalResourcePermissions.CreateKey,
        ExternalResourcePermissions.UpdateKey,
        ExternalResourcePermissions.DeleteKey,

        // Positions
        PositionPermissions.ReadKey,
        PositionPermissions.CreateKey,
        PositionPermissions.UpdateKey,
        PositionPermissions.DeleteKey,

        // Responsibilities
        ResponsibilityPermissions.ReadKey,
        ResponsibilityPermissions.CreateKey,
        ResponsibilityPermissions.UpdateKey,
        ResponsibilityPermissions.DeleteKey,

        // Risks
        RiskPermissions.ReadKey,
        RiskPermissions.CreateKey,
        RiskPermissions.UpdateKey,
        RiskPermissions.DeleteKey,
        RiskPermissions.ApproveKey,

        // Secret Providers
        SecretProviderPermissions.ReadKey,
        SecretProviderPermissions.CreateKey,
        SecretProviderPermissions.UpdateKey,
        SecretProviderPermissions.DeleteKey,

        // SQL Integrations
        SqlIntegrationPermissions.ReadKey,
        SqlIntegrationPermissions.CreateKey,
        SqlIntegrationPermissions.UpdateKey,
        SqlIntegrationPermissions.DeleteKey,
        SqlIntegrationPermissions.ApplyGrantsKey,

        // Kuma Integrations
        KumaIntegrationPermissions.ReadKey,
        KumaIntegrationPermissions.CreateKey,
        KumaIntegrationPermissions.UpdateKey,
        KumaIntegrationPermissions.DeleteKey,

        // Configuration
        ConfigPermissions.ExportKey,
        ConfigPermissions.ImportKey,

        // Audit
        AuditPermissions.ViewKey,

        // Users/Security
        UserAccountPermissions.ReadKey,
        UserAccountPermissions.CreateKey,
        UserAccountPermissions.UpdateKey,
        UserAccountPermissions.DeleteKey,
        RolePermissions.ReadKey,
        RolePermissions.CreateKey,
        RolePermissions.UpdateKey,
        RolePermissions.DeleteKey,
    };
}
