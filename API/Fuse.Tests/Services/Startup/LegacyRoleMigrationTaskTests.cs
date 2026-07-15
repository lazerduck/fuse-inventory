using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services.Startup;
using Fuse.Tests.TestInfrastructure;
using Xunit;

namespace Fuse.Tests.Services.Startup;

public class LegacyRoleMigrationTaskTests
{
    [Fact]
    public async Task RunAsync_AddsBuiltInRolesWithoutOverwritingExistingRoles()
    {
        var customRole = Guid.NewGuid();
        var admin = User(SecurityRole.Admin, [customRole]);
        var reader = User(SecurityRole.Reader, [customRole]);
        var readerWithAdminRole = User(SecurityRole.Reader, [BuiltInRoles.AdminRoleId]);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with
        {
            Security = s.Security with { Users = [admin, reader, readerWithAdminRole] }
        });

        var task = new LegacyRoleMigrationTask(store);
        await task.RunAsync();

        Assert.Equal(3, task.Order);
        Assert.Equal([customRole, BuiltInRoles.AdminRoleId], store.Current!.Security.Users[0].RoleIds);
        Assert.Equal([customRole, BuiltInRoles.ReaderRoleId], store.Current.Security.Users[1].RoleIds);
        Assert.Equal([BuiltInRoles.AdminRoleId], store.Current.Security.Users[2].RoleIds);
    }

    [Fact]
    public async Task RunAsync_IsIdempotentAndDoesNotPublishAnUnchangedSnapshot()
    {
        var user = User(SecurityRole.Admin, [BuiltInRoles.AdminRoleId]);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { Security = s.Security with { Users = [user] } });
        var changes = 0;
        store.Changed += _ => changes++;

        var task = new LegacyRoleMigrationTask(store);
        await task.RunAsync();
        await task.RunAsync();

        Assert.Equal(0, changes);
        Assert.Equal([BuiltInRoles.AdminRoleId], store.Current!.Security.Users.Single().RoleIds);
    }

    private static SecurityUser User(SecurityRole role, IReadOnlyList<Guid> roleIds) =>
        new(Guid.NewGuid(), "user", "hash", "salt", role, roleIds, DateTime.UtcNow, DateTime.UtcNow);
}
