using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Permissions;

public class SecurityServiceRoleTests
{
    private static InMemoryFuseStore NewStore(
        SecuritySettings? settings = null,
        IEnumerable<SecurityUser>? users = null,
        IEnumerable<Role>? roles = null)
    {
        var securityState = new SecurityState(
            settings ?? new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow),
            (users ?? Array.Empty<SecurityUser>()).ToArray(),
            (roles ?? Array.Empty<Role>()).ToArray()
        );

        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: Array.Empty<ExternalResource>(),
            Accounts: Array.Empty<Account>(),
            Identities: Array.Empty<Identity>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: Array.Empty<SecretProvider>(),
            SqlIntegrations: Array.Empty<SqlIntegration>(),
            Positions: Array.Empty<Position>(),
            ResponsibilityTypes: Array.Empty<ResponsibilityType>(),
            ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
            Security: securityState
        );

        return new InMemoryFuseStore(snapshot);
    }

    [Fact]
    public async Task CreateRoleAsync_DuplicateName_ReturnsConflict()
    {
        var existingRole = new Role(Guid.NewGuid(), "Reader", "", new[] { Permission.ApplicationsRead }, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(roles: new[] { existingRole });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.CreateRoleAsync(new CreateRole("reader", "", new[] { Permission.ApplicationsRead }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task UpdateRoleAsync_DefaultRole_ReturnsValidation()
    {
        var defaultAdminRole = new Role(
            PermissionService.DefaultAdminRoleId,
            "Administrator",
            "",
            new[] { Permission.ApplicationsRead },
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var store = NewStore(roles: new[] { defaultAdminRole });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.UpdateRoleAsync(new UpdateRole(defaultAdminRole.Id, "Admin", "", new[] { Permission.ApplicationsCreate }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task DeleteRoleAsync_AssignedToUser_ReturnsValidation()
    {
        var roleId = Guid.NewGuid();
        var role = new Role(roleId, "Ops", "", new[] { Permission.PlatformsRead }, DateTime.UtcNow, DateTime.UtcNow);
        var user = new SecurityUser(Guid.NewGuid(), "user", "hash", "salt", SecurityRole.Reader, new[] { roleId }, DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(users: new[] { user }, roles: new[] { role });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.DeleteRoleAsync(new DeleteRole(roleId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task AssignRolesToUserAsync_InvalidRole_ReturnsValidation()
    {
        var user = new SecurityUser(Guid.NewGuid(), "user", "hash", "salt", SecurityRole.Reader, DateTime.UtcNow, DateTime.UtcNow);
        var store = NewStore(users: new[] { user });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.AssignRolesToUserAsync(new AssignRolesToUser(user.Id, new[] { Guid.NewGuid() }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task AssignRolesToUserAsync_UpdatesRoleIds()
    {
        var roleA = new Role(Guid.NewGuid(), "RoleA", "", new[] { Permission.AccountsRead }, DateTime.UtcNow, DateTime.UtcNow);
        var roleB = new Role(Guid.NewGuid(), "RoleB", "", new[] { Permission.AccountsUpdate }, DateTime.UtcNow, DateTime.UtcNow);
        var user = new SecurityUser(Guid.NewGuid(), "user", "hash", "salt", SecurityRole.Reader, DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(users: new[] { user }, roles: new[] { roleA, roleB });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.AssignRolesToUserAsync(new AssignRolesToUser(user.Id, new[] { roleA.Id, roleB.Id }));

        Assert.True(result.IsSuccess);

        var state = await service.GetSecurityStateAsync();
        var updatedUser = state.Users.Single(u => u.Id == user.Id);
        Assert.Equal(2, updatedUser.RoleIds.Count);
        Assert.Contains(roleA.Id, updatedUser.RoleIds);
        Assert.Contains(roleB.Id, updatedUser.RoleIds);
    }

    [Fact]
    public async Task UpdateRoleAsync_UpdatesPermissionsAndDescription()
    {
        var roleId = Guid.NewGuid();
        var role = new Role(roleId, "Team", "Old", new[] { Permission.ApplicationsRead }, DateTime.UtcNow, DateTime.UtcNow);

        var store = NewStore(roles: new[] { role });
        var auditService = new FakeAuditService();
        var service = new SecurityService(store, auditService);

        var result = await service.UpdateRoleAsync(new UpdateRole(roleId, "Team", "New", new[] { Permission.ApplicationsRead, Permission.ApplicationsUpdate }));

        Assert.True(result.IsSuccess);
        Assert.Equal("New", result.Value!.Description);
        Assert.Contains(Permission.ApplicationsUpdate, result.Value.Permissions);
    }
}
