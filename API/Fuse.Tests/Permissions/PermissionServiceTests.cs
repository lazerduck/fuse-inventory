using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Moq;
using Xunit;

namespace Fuse.Tests.Permissions;

public class PermissionServiceTests
{
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
