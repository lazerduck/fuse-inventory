using Fuse.Core.Models;

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
    /// The SecurityRole to assign (Admin or Reader).
    /// </summary>
    public required SecurityRole Role { get; init; }

    /// <summary>
    /// Custom role ID to assign (optional, in addition to or instead of Role).
    /// Used for creating restricted custom roles.
    /// </summary>
    public Guid? CustomRoleId { get; init; }

    /// <summary>
    /// Specific permissions for custom roles (optional).
    /// If empty, uses permissions from the Role.
    /// </summary>
    public IReadOnlyList<Permission>? SpecificPermissions { get; init; }

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
    /// Required permission to access this endpoint (for documentation).
    /// </summary>
    public Permission? RequiredPermission { get; init; }

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
        Role = SecurityRole.Admin,
        DisplayName = "Admin User"
    };

    /// <summary>
    /// Reader user with read-only access.
    /// </summary>
    public static readonly TestUserScenario ReaderUser = new()
    {
        ScenarioId = "reader-user",
        UserName = "reader_test",
        Password = "ReaderPassword123!",
        Role = SecurityRole.Reader,
        DisplayName = "Reader User (Read-Only)"
    };

    /// <summary>
    /// No permissions user (custom role with no permissions, but still has Reader base role).
    /// Note: Reader role grants read access at the authentication level, even without specific permissions.
    /// </summary>
    public static readonly TestUserScenario NoPermissionsUser = new()
    {
        ScenarioId = "no-permissions-user",
        UserName = "restricted_test",
        Password = "RestrictedPassword123!",
        Role = SecurityRole.Reader,
        SpecificPermissions = Array.Empty<Permission>(),
        DisplayName = "Restricted User (Reader, No Custom Permissions)"
    };

    /// <summary>
    /// Limited permissions user (can read accounts, but not write).
    /// </summary>
    public static readonly TestUserScenario LimitedPermissionsUser = new()
    {
        ScenarioId = "limited-user",
        UserName = "limited_test",
        Password = "LimitedPassword123!",
        Role = SecurityRole.Reader,
        SpecificPermissions = new[]
        {
            Permission.AccountsRead,
            Permission.ApplicationsRead,
            Permission.IdentitiesRead,
            Permission.DataStoresRead,
            Permission.PlatformsRead,
            Permission.EnvironmentsRead,
            Permission.ExternalResourcesRead,
            Permission.PositionsRead,
            Permission.ResponsibilitiesRead,
            Permission.RisksRead,
            Permission.UsersRead,
            Permission.RolesRead,
            Permission.AuditLogsView
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
        Role = SecurityRole.Reader,
        SpecificPermissions = new[]
        {
            Permission.AccountsRead,
            Permission.AccountsCreate,
            Permission.AccountsUpdate,
            Permission.ApplicationsRead,
            Permission.ApplicationsCreate,
            Permission.ApplicationsUpdate,
            Permission.IdentitiesRead,
            Permission.IdentitiesCreate,
            Permission.IdentitiesUpdate,
            Permission.DataStoresRead,
            Permission.DataStoresCreate,
            Permission.DataStoresUpdate,
            Permission.PlatformsRead,
            Permission.PlatformsCreate,
            Permission.PlatformsUpdate,
            Permission.EnvironmentsRead,
            Permission.EnvironmentsCreate,
            Permission.EnvironmentsUpdate,
            Permission.ExternalResourcesRead,
            Permission.ExternalResourcesCreate,
            Permission.ExternalResourcesUpdate,
            Permission.PositionsRead,
            Permission.PositionsCreate,
            Permission.PositionsUpdate,
            Permission.ResponsibilitiesRead,
            Permission.ResponsibilitiesCreate,
            Permission.ResponsibilitiesUpdate,
            Permission.RisksRead,
            Permission.RisksCreate,
            Permission.RisksUpdate,
            Permission.UsersRead,
            Permission.RolesRead,
            Permission.RisksApprove,
            Permission.AuditLogsView
        },
        DisplayName = "Power User (Create/Read/Update)"
    };

    /// <summary>
    /// User with only user management permissions (no roles/audit/config).
    /// Tests isolation of UsersRead/Create/Update/Delete permissions.
    /// </summary>
    public static readonly TestUserScenario UserManagerUser = new()
    {
        ScenarioId = "user-manager",
        UserName = "usermgr_test",
        Password = "UserMgrPassword123!",
        Role = SecurityRole.Reader,
        SpecificPermissions = new[]
        {
            Permission.UsersRead,
            Permission.UsersCreate,
            Permission.UsersUpdate,
            Permission.UsersDelete
        },
        DisplayName = "User Manager (Users Only)"
    };

    /// <summary>
    /// User with only role management permissions (no users/audit/config).
    /// Tests isolation of RolesRead/Create/Update/Delete permissions.
    /// </summary>
    public static readonly TestUserScenario RoleManagerUser = new()
    {
        ScenarioId = "role-manager",
        UserName = "rolemgr_test",
        Password = "RoleMgrPassword123!",
        Role = SecurityRole.Reader,
        SpecificPermissions = new[]
        {
            Permission.RolesRead,
            Permission.RolesCreate,
            Permission.RolesUpdate,
            Permission.RolesDelete
        },
        DisplayName = "Role Manager (Roles Only)"
    };

    /// <summary>
    /// User with only read permissions for users and roles (no write).
    /// Tests read-only access to security management.
    /// </summary>
    public static readonly TestUserScenario SecurityReaderUser = new()
    {
        ScenarioId = "security-reader",
        UserName = "secreader_test",
        Password = "SecReaderPassword123!",
        Role = SecurityRole.Reader,
        SpecificPermissions = new[]
        {
            Permission.UsersRead,
            Permission.RolesRead
        },
        DisplayName = "Security Reader (Read-Only)"
    };

    /// <summary>
    /// User with audit log viewing and configuration export permissions.
    /// Tests access to sensitive read-only operations.
    /// </summary>
    public static readonly TestUserScenario AuditorUser = new()
    {
        ScenarioId = "auditor",
        UserName = "auditor_test",
        Password = "AuditorPassword123!",
        Role = SecurityRole.Reader,
        SpecificPermissions = new[]
        {
            Permission.AuditLogsView,
            Permission.ConfigurationExport
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
            RequiredPermission = Permission.AccountsRead
        },
        new()
        {
            HttpMethod = "POST",
            Endpoint = "/api/account",
            DisplayName = "Create Account",
            RequestBody = new { name = "TestAccount", description = "Test" },
            ExpectedStatusCode = 201,
            RequiredPermission = Permission.AccountsCreate
        },

        // Application endpoints
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/application",
            DisplayName = "Get Applications",
            ExpectedStatusCode = 200,
            RequiredPermission = Permission.ApplicationsRead
        },
        new()
        {
            HttpMethod = "POST",
            Endpoint = "/api/application",
            DisplayName = "Create Application",
            RequestBody = new { name = "TestApp", description = "Test" },
            ExpectedStatusCode = 201,
            RequiredPermission = Permission.ApplicationsCreate
        },

        // Audit logs (sensitive - read-only, but permission-gated)
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/audit",
            DisplayName = "Get Audit Logs",
            ExpectedStatusCode = 200,
            RequiredPermission = Permission.AuditLogsView
        },

        // Config export (highly sensitive - requires specific permission)
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/config/export",
            DisplayName = "Export Configuration",
            ExpectedStatusCode = 200,
            RequiredPermission = Permission.ConfigurationExport
        },

        // Security endpoints - User management (CRUD with granular permissions)
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/security/accounts",
            DisplayName = "Get Security Accounts",
            ExpectedStatusCode = 200,
            RequiredPermission = Permission.UsersRead
        },
        new()
        {
            HttpMethod = "POST",
            Endpoint = "/api/security/accounts",
            DisplayName = "Create Security Account",
            RequestBody = new { userName = "newuser", password = "NewPassword123!", role = "Reader" },
            ExpectedStatusCode = 201,
            RequiredPermission = Permission.UsersCreate
        },
        new()
        {
            HttpMethod = "PATCH",
            Endpoint = "/api/security/accounts/placeholder-id",
            DisplayName = "Update Security Account",
            RequestBody = new { userName = "updateduser" },
            ExpectedStatusCode = 200,
            RequiredPermission = Permission.UsersUpdate
        },
        new()
        {
            HttpMethod = "DELETE",
            Endpoint = "/api/security/accounts/placeholder-id",
            DisplayName = "Delete Security Account",
            ExpectedStatusCode = 204,
            RequiredPermission = Permission.UsersDelete
        },

        // Role management endpoints (CRUD with granular permissions)
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/roles",
            DisplayName = "Get Roles",
            ExpectedStatusCode = 200,
            RequiredPermission = Permission.RolesRead
        },
        new()
        {
            HttpMethod = "POST",
            Endpoint = "/api/roles",
            DisplayName = "Create Role",
            RequestBody = new { name = "NewRole", permissions = new string[] { } },
            ExpectedStatusCode = 201,
            RequiredPermission = Permission.RolesCreate
        },
        new()
        {
            HttpMethod = "PUT",
            Endpoint = "/api/roles/placeholder-id",
            DisplayName = "Update Role",
            RequestBody = new { name = "UpdatedRole" },
            ExpectedStatusCode = 200,
            RequiredPermission = Permission.RolesUpdate
        },
        new()
        {
            HttpMethod = "DELETE",
            Endpoint = "/api/roles/placeholder-id",
            DisplayName = "Delete Role",
            ExpectedStatusCode = 204,
            RequiredPermission = Permission.RolesDelete
        },

        // Positions endpoints
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/position",
            DisplayName = "Get Positions",
            ExpectedStatusCode = 200,
            RequiredPermission = Permission.PositionsRead
        },

        // Identities endpoints
        new()
        {
            HttpMethod = "GET",
            Endpoint = "/api/identity",
            DisplayName = "Get Identities",
            ExpectedStatusCode = 200,
            RequiredPermission = Permission.IdentitiesRead
        },
    };

    /// <summary>
    /// Get expected status codes for a user scenario and endpoint test.
    /// Applies permission inference logic.
    /// </summary>
    public static int GetExpectedStatusCode(TestUserScenario scenario, EndpointAccessTest test)
    {
        // Admin always gets access (except for 404s due to missing resources)
        if (scenario.Role == SecurityRole.Admin)
        {
            return test.ExpectedStatusCode;
        }

        // For endpoints that require specific permissions, check if user has them
        if (test.RequiredPermission.HasValue)
        {
            var permissions = scenario.SpecificPermissions;
            
            // If SpecificPermissions is not null, the user has a custom role (even if empty)
            // Custom roles override default role permissions
            if (permissions != null)
            {
                // User has custom role - only check those permissions
                if (!permissions.Contains(test.RequiredPermission.Value))
                {
                    return 403; // Forbidden - missing required permission in custom role
                }
                // User has the required permission - return expected status
                return test.ExpectedStatusCode;
            }
            else if (scenario.Role == SecurityRole.Reader)
            {
                // Reader users with NO custom roles get default reader permissions
                var defaultPermissions = GetReaderPermissions();
                if (!defaultPermissions.Contains(test.RequiredPermission.Value))
                {
                    return 403; // Forbidden - permission not in default reader set
                }
                // User has the required permission - return expected status
                return test.ExpectedStatusCode;
            }
            else
            {
                // Non-reader, non-admin users without permissions can't access
                return 403;
            }
        }

        // For regular CRUD endpoints with SecurityLevel.FullyRestricted:
        // - GET/HEAD/OPTIONS: Any authenticated user can access (Reader or Admin)
        // - POST/PUT/DELETE/PATCH: Only Admin can access
        var isReadOperation = test.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
                              test.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase) ||
                              test.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase);

        if (!isReadOperation)
        {
            // Write operations require Admin role
            if (scenario.Role != SecurityRole.Admin)
            {
                return 403; // Non-admin users get 403 on write operations
            }
        }

        // Reader users can perform read operations (if no specific permission required)
        // Return the expected status from the test
        return test.ExpectedStatusCode;
    }

    /// <summary>
    /// Get the default permissions for a security role.
    /// </summary>
    public static IReadOnlyList<Permission> GetDefaultPermissions(SecurityRole role)
    {
        return role switch
        {
            SecurityRole.Admin => GetAllPermissions(),
            SecurityRole.Reader => GetReaderPermissions(),
            _ => Array.Empty<Permission>()
        };
    }

    /// <summary>
    /// Get all available permissions.
    /// </summary>
    public static IReadOnlyList<Permission> GetAllPermissions() => new[]
    {
        // Applications
        Permission.ApplicationsRead,
        Permission.ApplicationsCreate,
        Permission.ApplicationsUpdate,
        Permission.ApplicationsDelete,

        // Accounts
        Permission.AccountsRead,
        Permission.AccountsCreate,
        Permission.AccountsUpdate,
        Permission.AccountsDelete,

        // Identities
        Permission.IdentitiesRead,
        Permission.IdentitiesCreate,
        Permission.IdentitiesUpdate,
        Permission.IdentitiesDelete,

        // DataStores
        Permission.DataStoresRead,
        Permission.DataStoresCreate,
        Permission.DataStoresUpdate,
        Permission.DataStoresDelete,

        // Platforms
        Permission.PlatformsRead,
        Permission.PlatformsCreate,
        Permission.PlatformsUpdate,
        Permission.PlatformsDelete,

        // Environments
        Permission.EnvironmentsRead,
        Permission.EnvironmentsCreate,
        Permission.EnvironmentsUpdate,
        Permission.EnvironmentsDelete,

        // ExternalResources
        Permission.ExternalResourcesRead,
        Permission.ExternalResourcesCreate,
        Permission.ExternalResourcesUpdate,
        Permission.ExternalResourcesDelete,

        // Positions
        Permission.PositionsRead,
        Permission.PositionsCreate,
        Permission.PositionsUpdate,
        Permission.PositionsDelete,

        // Responsibilities
        Permission.ResponsibilitiesRead,
        Permission.ResponsibilitiesCreate,
        Permission.ResponsibilitiesUpdate,
        Permission.ResponsibilitiesDelete,

        // Risks
        Permission.RisksRead,
        Permission.RisksCreate,
        Permission.RisksUpdate,
        Permission.RisksDelete,
        Permission.RisksApprove,

        // Azure Key Vault
        Permission.AzureKeyVaultSecretsView,
        Permission.AzureKeyVaultConnectionsCreate,
        Permission.AzureKeyVaultConnectionsDelete,

        // SQL
        Permission.SqlConnectionsCreate,
        Permission.SqlConnectionsDelete,
        Permission.SqlGrantsApply,

        // Kuma
        Permission.KumaIntegrationsCreate,
        Permission.KumaIntegrationsDelete,

        // Configuration
        Permission.ConfigurationExport,
        Permission.ConfigurationImport,

        // Audit
        Permission.AuditLogsView,

        // Users/Security
        Permission.UsersRead,
        Permission.UsersCreate,
        Permission.UsersUpdate,
        Permission.UsersDelete,
        Permission.RolesRead,
        Permission.RolesCreate,
        Permission.RolesUpdate,
        Permission.RolesDelete
    };

    /// <summary>
    /// Get the default permissions for the Reader role.
    /// </summary>
    public static IReadOnlyList<Permission> GetReaderPermissions() => new[]
    {
        Permission.ApplicationsRead,
        Permission.AccountsRead,
        Permission.IdentitiesRead,
        Permission.DataStoresRead,
        Permission.PlatformsRead,
        Permission.EnvironmentsRead,
        Permission.ExternalResourcesRead,
        Permission.PositionsRead,
        Permission.ResponsibilitiesRead,
        Permission.RisksRead,
        Permission.UsersRead,
        Permission.RolesRead,

    };
}
