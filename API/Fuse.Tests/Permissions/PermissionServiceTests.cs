using Fuse.API;
using Fuse.API.Controllers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Moq;
using System.Reflection;
using Xunit;

namespace Fuse.Tests.Permissions;

public class PermissionServiceTests
{
    // Verify that key controller actions declare the correct [RequirePermission] attribute.
    // Format: (controllerType, methodName, expectedPermission)
    public static readonly object[][] ControllerPermissionMappings =
    [
        // ApplicationController
        new object[] { typeof(ApplicationController), "GetApplications", Permission.ApplicationsRead },
        new object[] { typeof(ApplicationController), "GetApplicationById", Permission.ApplicationsRead },
        new object[] { typeof(ApplicationController), "CreateApplication", Permission.ApplicationsCreate },
        new object[] { typeof(ApplicationController), "UpdateApplication", Permission.ApplicationsUpdate },
        new object[] { typeof(ApplicationController), "DeleteApplication", Permission.ApplicationsDelete },
        new object[] { typeof(ApplicationController), "CreateInstance", Permission.ApplicationsCreate },
        new object[] { typeof(ApplicationController), "UpdateInstance", Permission.ApplicationsUpdate },
        new object[] { typeof(ApplicationController), "DeleteInstance", Permission.ApplicationsDelete },
        new object[] { typeof(ApplicationController), "GetInstanceHealth", Permission.ApplicationsRead },
        new object[] { typeof(ApplicationController), "GetInstanceApiKey", Permission.ApplicationsRead },
        new object[] { typeof(ApplicationController), "CreatePipeline", Permission.ApplicationsCreate },
        new object[] { typeof(ApplicationController), "UpdatePipeline", Permission.ApplicationsUpdate },
        new object[] { typeof(ApplicationController), "DeletePipeline", Permission.ApplicationsDelete },
        new object[] { typeof(ApplicationController), "CreateDependency", Permission.ApplicationsCreate },
        new object[] { typeof(ApplicationController), "UpdateDependency", Permission.ApplicationsUpdate },
        new object[] { typeof(ApplicationController), "DeleteDependency", Permission.ApplicationsDelete },

        // AccountController
        new object[] { typeof(AccountController), "GetAccounts", Permission.AccountsRead },
        new object[] { typeof(AccountController), "GetAccountById", Permission.AccountsRead },
        new object[] { typeof(AccountController), "GetAccountSqlStatus", Permission.AccountsRead },
        new object[] { typeof(AccountController), "CreateAccount", Permission.AccountsCreate },
        new object[] { typeof(AccountController), "UpdateAccount", Permission.AccountsUpdate },
        new object[] { typeof(AccountController), "DeleteAccount", Permission.AccountsDelete },

        // AuditController
        new object[] { typeof(AuditController), "QueryAuditLogs", Permission.AuditLogsView },
        new object[] { typeof(AuditController), "GetAuditLog", Permission.AuditLogsView },

        // ActivityController
        new object[] { typeof(ActivityController), "Query", Permission.ActivityRead },
        new object[] { typeof(ActivityController), "QueryByEntity", Permission.ActivityRead },

        // ConfigController
        new object[] { typeof(ConfigController), "Export", Permission.ConfigurationExport },
        new object[] { typeof(ConfigController), "Import", Permission.ConfigurationImport },
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

    [Theory]
    [MemberData(nameof(ControllerPermissionMappings))]
    public void ControllerAction_HasExpectedRequirePermissionAttribute(Type controllerType, string methodName, Permission expectedPermission)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        var attr = method!.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(expectedPermission, attr!.Permission);
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
            Permission.MessageBrokersRead,
            Permission.PositionsRead,
            Permission.ResponsibilitiesRead,
            Permission.RisksRead,
            Permission.ActivityRead,
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
