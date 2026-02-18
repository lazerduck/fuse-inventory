using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Moq;
using Xunit;

namespace Fuse.Tests.Permissions;

public class PermissionServiceTests
{
    public static readonly object[][] PermissionMappings =
    [
        new object[] { "Application", "GetAll", "GET", Permission.ApplicationsRead },
        new object[] { "Application", "Get", "GET", Permission.ApplicationsRead },
        new object[] { "Application", "Create", "POST", Permission.ApplicationsCreate },
        new object[] { "Application", "Update", "PUT", Permission.ApplicationsUpdate },
        new object[] { "Application", "Update", "PATCH", Permission.ApplicationsUpdate },
        new object[] { "Application", "Delete", "DELETE", Permission.ApplicationsDelete },

        new object[] { "Account", "GetAll", "GET", Permission.AccountsRead },
        new object[] { "Account", "Get", "GET", Permission.AccountsRead },
        new object[] { "Account", "Create", "POST", Permission.AccountsCreate },
        new object[] { "Account", "Update", "PUT", Permission.AccountsUpdate },
        new object[] { "Account", "Update", "PATCH", Permission.AccountsUpdate },
        new object[] { "Account", "Delete", "DELETE", Permission.AccountsDelete },
        new object[] { "Account", "ApplyGrants", "POST", Permission.SqlGrantsApply },

        new object[] { "Identity", "GetAll", "GET", Permission.IdentitiesRead },
        new object[] { "Identity", "Get", "GET", Permission.IdentitiesRead },
        new object[] { "Identity", "Create", "POST", Permission.IdentitiesCreate },
        new object[] { "Identity", "Update", "PUT", Permission.IdentitiesUpdate },
        new object[] { "Identity", "Update", "PATCH", Permission.IdentitiesUpdate },
        new object[] { "Identity", "Delete", "DELETE", Permission.IdentitiesDelete },

        new object[] { "DataStore", "GetAll", "GET", Permission.DataStoresRead },
        new object[] { "DataStore", "Get", "GET", Permission.DataStoresRead },
        new object[] { "DataStore", "Create", "POST", Permission.DataStoresCreate },
        new object[] { "DataStore", "Update", "PUT", Permission.DataStoresUpdate },
        new object[] { "DataStore", "Update", "PATCH", Permission.DataStoresUpdate },
        new object[] { "DataStore", "Delete", "DELETE", Permission.DataStoresDelete },

        new object[] { "Platform", "GetAll", "GET", Permission.PlatformsRead },
        new object[] { "Platform", "Get", "GET", Permission.PlatformsRead },
        new object[] { "Platform", "Create", "POST", Permission.PlatformsCreate },
        new object[] { "Platform", "Update", "PUT", Permission.PlatformsUpdate },
        new object[] { "Platform", "Update", "PATCH", Permission.PlatformsUpdate },
        new object[] { "Platform", "Delete", "DELETE", Permission.PlatformsDelete },

        new object[] { "Environment", "GetAll", "GET", Permission.EnvironmentsRead },
        new object[] { "Environment", "Get", "GET", Permission.EnvironmentsRead },
        new object[] { "Environment", "Create", "POST", Permission.EnvironmentsCreate },
        new object[] { "Environment", "Update", "PUT", Permission.EnvironmentsUpdate },
        new object[] { "Environment", "Update", "PATCH", Permission.EnvironmentsUpdate },
        new object[] { "Environment", "Delete", "DELETE", Permission.EnvironmentsDelete },

        new object[] { "ExternalResource", "GetAll", "GET", Permission.ExternalResourcesRead },
        new object[] { "ExternalResource", "Get", "GET", Permission.ExternalResourcesRead },
        new object[] { "ExternalResource", "Create", "POST", Permission.ExternalResourcesCreate },
        new object[] { "ExternalResource", "Update", "PUT", Permission.ExternalResourcesUpdate },
        new object[] { "ExternalResource", "Update", "PATCH", Permission.ExternalResourcesUpdate },
        new object[] { "ExternalResource", "Delete", "DELETE", Permission.ExternalResourcesDelete },

        new object[] { "Position", "GetAll", "GET", Permission.PositionsRead },
        new object[] { "Position", "Get", "GET", Permission.PositionsRead },
        new object[] { "Position", "Create", "POST", Permission.PositionsCreate },
        new object[] { "Position", "Update", "PUT", Permission.PositionsUpdate },
        new object[] { "Position", "Update", "PATCH", Permission.PositionsUpdate },
        new object[] { "Position", "Delete", "DELETE", Permission.PositionsDelete },

        new object[] { "ResponsibilityType", "GetAll", "GET", Permission.ResponsibilitiesRead },
        new object[] { "ResponsibilityType", "Get", "GET", Permission.ResponsibilitiesRead },
        new object[] { "ResponsibilityType", "Create", "POST", Permission.ResponsibilitiesCreate },
        new object[] { "ResponsibilityType", "Update", "PUT", Permission.ResponsibilitiesUpdate },
        new object[] { "ResponsibilityType", "Update", "PATCH", Permission.ResponsibilitiesUpdate },
        new object[] { "ResponsibilityType", "Delete", "DELETE", Permission.ResponsibilitiesDelete },

        new object[] { "ResponsibilityAssignment", "GetAll", "GET", Permission.ResponsibilitiesRead },
        new object[] { "ResponsibilityAssignment", "Get", "GET", Permission.ResponsibilitiesRead },
        new object[] { "ResponsibilityAssignment", "Create", "POST", Permission.ResponsibilitiesCreate },
        new object[] { "ResponsibilityAssignment", "Update", "PUT", Permission.ResponsibilitiesUpdate },
        new object[] { "ResponsibilityAssignment", "Update", "PATCH", Permission.ResponsibilitiesUpdate },
        new object[] { "ResponsibilityAssignment", "Delete", "DELETE", Permission.ResponsibilitiesDelete },

        new object[] { "Risk", "GetAll", "GET", Permission.RisksRead },
        new object[] { "Risk", "Get", "GET", Permission.RisksRead },
        new object[] { "Risk", "Create", "POST", Permission.RisksCreate },
        new object[] { "Risk", "Update", "PUT", Permission.RisksUpdate },
        new object[] { "Risk", "Update", "PATCH", Permission.RisksUpdate },
        new object[] { "Risk", "Delete", "DELETE", Permission.RisksDelete },
        new object[] { "Risk", "Approve", "POST", Permission.RisksApprove },

        new object[] { "SecretProvider", "GetSecrets", "GET", Permission.AzureKeyVaultSecretsView },
        new object[] { "SecretProvider", "Create", "POST", Permission.AzureKeyVaultConnectionsCreate },
        new object[] { "SecretProvider", "Delete", "DELETE", Permission.AzureKeyVaultConnectionsDelete },

        new object[] { "SqlIntegration", "Create", "POST", Permission.SqlConnectionsCreate },
        new object[] { "SqlIntegration", "Delete", "DELETE", Permission.SqlConnectionsDelete },

        new object[] { "KumaIntegration", "Create", "POST", Permission.KumaIntegrationsCreate },
        new object[] { "KumaIntegration", "Delete", "DELETE", Permission.KumaIntegrationsDelete },

        new object[] { "Config", "Export", "GET", Permission.ConfigurationExport },
        new object[] { "Config", "Import", "POST", Permission.ConfigurationImport },

        new object[] { "Audit", "GetAll", "GET", Permission.AuditLogsView },
        new object[] { "Audit", "Get", "GET", Permission.AuditLogsView },

        new object[] { "Security", "GetAccounts", "GET", Permission.UsersRead },
        new object[] { "Security", "CreateAccount", "POST", Permission.UsersCreate },
        new object[] { "Security", "UpdateUser", "PATCH", Permission.UsersUpdate },
        new object[] { "Security", "DeleteUser", "DELETE", Permission.UsersDelete }
    ];

    [Fact]
    public async Task GetUserPermissionsAsync_AdminRole_ReturnsAllPermissions()
    {
        var securityService = new Mock<ISecurityService>();
        var service = new PermissionService(securityService.Object);
        var user = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);

        var permissions = await service.GetUserPermissionsAsync(user);

        Assert.Equal(Enum.GetValues<Permission>().Length, permissions.Count);
        Assert.Contains(Permission.ApplicationsCreate, permissions);
        Assert.Contains(Permission.RolesDelete, permissions);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_ReaderNoRoles_ReturnsDefaultReaderPermissions()
    {
        var securityService = new Mock<ISecurityService>();
        var service = new PermissionService(securityService.Object);
        var user = new SecurityUser(Guid.NewGuid(), "reader", "hash", "salt", SecurityRole.Reader, DateTime.UtcNow, DateTime.UtcNow);

        var permissions = await service.GetUserPermissionsAsync(user);

        var readerPermissions = service.GetDefaultReaderRole().Permissions;
        Assert.Equal(readerPermissions.Count, permissions.Count);
        Assert.All(readerPermissions, p => Assert.Contains(p, permissions));
        Assert.DoesNotContain(Permission.ApplicationsCreate, permissions);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_AssignedRoles_ReturnsUnion()
    {
        var roleA = new Role(Guid.NewGuid(), "RoleA", "", new[] { Permission.ApplicationsRead, Permission.ApplicationsCreate }, DateTime.UtcNow, DateTime.UtcNow);
        var roleB = new Role(Guid.NewGuid(), "RoleB", "", new[] { Permission.AccountsRead }, DateTime.UtcNow, DateTime.UtcNow);
        var user = new SecurityUser(Guid.NewGuid(), "user", "hash", "salt", SecurityRole.Reader, new[] { roleA.Id, roleB.Id }, DateTime.UtcNow, DateTime.UtcNow);

        var state = new SecurityState(
            new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow),
            new[] { user },
            new[] { roleA, roleB }
        );

        var securityService = new Mock<ISecurityService>();
        securityService
            .Setup(s => s.GetSecurityStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        var service = new PermissionService(securityService.Object);

        var permissions = await service.GetUserPermissionsAsync(user);

        Assert.Contains(Permission.ApplicationsRead, permissions);
        Assert.Contains(Permission.ApplicationsCreate, permissions);
        Assert.Contains(Permission.AccountsRead, permissions);
    }

    [Fact]
    public void GetRequiredPermission_ReturnsMappedPermissionOrNull()
    {
        var securityService = new Mock<ISecurityService>();
        var service = new PermissionService(securityService.Object);

        var mapped = service.GetRequiredPermission("Application", "GetAll", "GET");
        var missing = service.GetRequiredPermission("Unknown", "Action", "GET");

        Assert.Equal(Permission.ApplicationsRead, mapped);
        Assert.Null(missing);
    }

    [Theory]
    [MemberData(nameof(PermissionMappings))]
    public void GetRequiredPermission_ReturnsExpectedMapping(string controller, string action, string method, Permission expected)
    {
        var securityService = new Mock<ISecurityService>();
        var service = new PermissionService(securityService.Object);

        var permission = service.GetRequiredPermission(controller, action, method);

        Assert.Equal(expected, permission);
    }

    [Fact]
    public async Task EnsureDefaultRolesAsync_AddsMissingDefaults()
    {
        var state = new SecurityState(
            new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow),
            Array.Empty<SecurityUser>(),
            Array.Empty<Role>()
        );

        var securityService = new Mock<ISecurityService>();
        securityService
            .Setup(s => s.GetSecurityStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        var service = new PermissionService(securityService.Object);

        var roles = await service.EnsureDefaultRolesAsync();

        Assert.Contains(roles, r => r.Id == PermissionService.DefaultAdminRoleId);
        Assert.Contains(roles, r => r.Id == PermissionService.DefaultReaderRoleId);
    }

    [Fact]
    public void DefaultAdminRole_IncludesAllPermissions()
    {
        var securityService = new Mock<ISecurityService>();
        var service = new PermissionService(securityService.Object);

        var adminRole = service.GetDefaultAdminRole();

        Assert.Equal(Enum.GetValues<Permission>().Length, adminRole.Permissions.Count);
        Assert.Contains(Permission.RisksApprove, adminRole.Permissions);
        Assert.Contains(Permission.ConfigurationExport, adminRole.Permissions);
        Assert.Contains(Permission.AzureKeyVaultSecretsView, adminRole.Permissions);
    }

    [Fact]
    public void DefaultReaderRole_ContainsReadOnlyPermissions()
    {
        var securityService = new Mock<ISecurityService>();
        var service = new PermissionService(securityService.Object);

        var readerRole = service.GetDefaultReaderRole();

        var expected = new[]
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
            Permission.RolesRead
        };

        Assert.Equal(expected.Length, readerRole.Permissions.Count);
        Assert.All(expected, p => Assert.Contains(p, readerRole.Permissions));
        Assert.DoesNotContain(Permission.ApplicationsCreate, readerRole.Permissions);
        Assert.DoesNotContain(Permission.RisksApprove, readerRole.Permissions);
        Assert.DoesNotContain(Permission.ConfigurationExport, readerRole.Permissions);
        Assert.DoesNotContain(Permission.AzureKeyVaultSecretsView, readerRole.Permissions);
    }

    [Fact]
    public void DefaultReaderRole_DoesNotContainAnyNonReadPermissions()
    {
        var securityService = new Mock<ISecurityService>();
        var service = new PermissionService(securityService.Object);
        var readerRole = service.GetDefaultReaderRole();

        foreach (var permission in Enum.GetValues<Permission>())
        {
            var isReadPermission = permission.ToString().EndsWith("Read", StringComparison.Ordinal) ||
                                   permission is Permission.UsersRead or Permission.RolesRead;

            if (isReadPermission)
            {
                Assert.Contains(permission, readerRole.Permissions);
            }
            else
            {
                Assert.DoesNotContain(permission, readerRole.Permissions);
            }
        }
    }

    [Fact]
    public async Task GetUserPermissionsAsync_MultipleSpecialRoles_CombinesPermissions()
    {
        var riskApprover = new Role(Guid.NewGuid(), "RiskApprover", "", new[] { Permission.RisksApprove }, DateTime.UtcNow, DateTime.UtcNow);
        var secretsViewer = new Role(Guid.NewGuid(), "SecretsViewer", "", new[] { Permission.AzureKeyVaultSecretsView }, DateTime.UtcNow, DateTime.UtcNow);
        var configExporter = new Role(Guid.NewGuid(), "ConfigExporter", "", new[] { Permission.ConfigurationExport }, DateTime.UtcNow, DateTime.UtcNow);
        var sqlGrants = new Role(Guid.NewGuid(), "SqlGrants", "", new[] { Permission.SqlGrantsApply }, DateTime.UtcNow, DateTime.UtcNow);

        var user = new SecurityUser(
            Guid.NewGuid(),
            "user",
            "hash",
            "salt",
            SecurityRole.Reader,
            new[] { riskApprover.Id, secretsViewer.Id, configExporter.Id, sqlGrants.Id },
            DateTime.UtcNow,
            DateTime.UtcNow);

        var state = new SecurityState(
            new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow),
            new[] { user },
            new[] { riskApprover, secretsViewer, configExporter, sqlGrants }
        );

        var securityService = new Mock<ISecurityService>();
        securityService
            .Setup(s => s.GetSecurityStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        var service = new PermissionService(securityService.Object);

        var permissions = await service.GetUserPermissionsAsync(user);

        Assert.Contains(Permission.RisksApprove, permissions);
        Assert.Contains(Permission.AzureKeyVaultSecretsView, permissions);
        Assert.Contains(Permission.ConfigurationExport, permissions);
        Assert.Contains(Permission.SqlGrantsApply, permissions);
    }
}
