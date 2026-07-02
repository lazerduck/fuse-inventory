using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Security.Services;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class SecurityUserAndSessionServiceTests
{
    private static FuseRole Role() => new(Guid.NewGuid(), "Role", "", [], DateTime.UtcNow, DateTime.UtcNow);
    private static FuseUser User() => new(Guid.NewGuid(), "user", "hash", "salt", false, [], DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1));

    [Fact]
    public async Task CreateAndVerifyUser_ValidateHashAndNormalizeInput()
    {
        var store = new InMemoryFuseStore();
        var role = Role();
        var roles = new Mock<IFuseRoleService>();
        var service = new FuseUserService(store, roles.Object);

        Assert.Equal(ErrorType.Validation, (await service.CreateUser(" ", "password", false, [])).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.CreateUser("user", " ", false, [])).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.CreateUser("user", "password", false, null!)).ErrorType);
        roles.Setup(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>())).ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Failure("missing", ErrorType.NotFound));
        Assert.Equal(ErrorType.NotFound, (await service.CreateUser("user", "password", false, [role.Id])).ErrorType);
        roles.Setup(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>())).ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success([role]));

        var created = await service.CreateUser(" user ", "password", true, [role.Id, role.Id]);
        Assert.True(created.IsSuccess);
        Assert.Equal("user", created.Value!.UserName);
        Assert.True(created.Value.IsAdmin);
        Assert.Equal([role.Id], created.Value.RoleIds);
        Assert.True((await service.VerifyUser("USER", "password")).IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, (await service.VerifyUser("user", "wrong")).ErrorType);
        Assert.Equal(ErrorType.Unauthorized, (await service.VerifyUser("missing", "password")).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.VerifyUser(" ", "password")).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.VerifyUser("user", " ")).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await service.CreateUser("USER", "password", false, [])).ErrorType);
    }

    [Fact]
    public async Task UserQueriesRolesPasswordAndDeleteBehaviors()
    {
        var store = new InMemoryFuseStore();
        var role = Role();
        var roles = new Mock<IFuseRoleService>();
        roles.Setup(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>())).ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success([role]));
        var service = new FuseUserService(store, roles.Object);
        var created = (await service.CreateUser("user", "old-password", false, [])).Value!;

        Assert.Equal([created], (await service.GetUsers()).Value);
        Assert.Equal(ErrorType.Validation, (await service.GetUser(Guid.Empty)).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await service.GetUser(Guid.NewGuid())).ErrorType);
        Assert.Equal(created, (await service.GetUser(created.Id)).Value);
        Assert.Equal(ErrorType.Validation, (await service.SetUserRoles(Guid.Empty, [])).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.SetUserRoles(created.Id, null!)).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await service.SetUserRoles(Guid.NewGuid(), [])).ErrorType);
        roles.Setup(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>())).ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Failure("missing", ErrorType.NotFound));
        Assert.Equal(ErrorType.NotFound, (await service.SetUserRoles(created.Id, [role.Id])).ErrorType);
        roles.Setup(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>())).ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success([role]));
        Assert.True((await service.SetUserRoles(created.Id, [role.Id, role.Id])).IsSuccess);
        Assert.Equal([role.Id], store.Current!.SecurityContext.Users.Single().RoleIds);

        Assert.Equal(ErrorType.Validation, (await service.ResetPassword(Guid.Empty, "new-password")).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.ResetPassword(created.Id, " ")).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await service.ResetPassword(Guid.NewGuid(), "new-password")).ErrorType);
        Assert.True((await service.ResetPassword(created.Id, "new-password")).IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, (await service.VerifyUser("user", "old-password")).ErrorType);
        Assert.True((await service.VerifyUser("user", "new-password")).IsSuccess);

        Assert.Equal(ErrorType.Validation, (await service.DeleteUser(Guid.Empty)).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await service.DeleteUser(Guid.NewGuid())).ErrorType);
        Assert.True((await service.DeleteUser(created.Id)).IsSuccess);
        Assert.Empty(store.Current.SecurityContext.Users);
    }

    [Fact]
    public async Task Sessions_CreateValidateRefreshAndDelete()
    {
        var store = new InMemoryFuseStore();
        var service = new FuseUserSessionService(store);
        var user = User();

        Assert.Equal(ErrorType.Validation, (await service.CreateSession(null!)).ErrorType);
        var created = await service.CreateSession(user);
        Assert.True(created.IsSuccess);
        Assert.Equal(user.Id, (await service.ValidateSession(created.Value!)).Value);
        Assert.True((await service.GetExpiry(created.Value!)).Value > DateTime.UtcNow);
        Assert.Equal(ErrorType.Validation, (await service.ValidateSession(" ")).ErrorType);
        Assert.Equal(ErrorType.Unauthorized, (await service.ValidateSession("missing")).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await service.GetExpiry("missing")).ErrorType);
        Assert.Equal(ErrorType.Validation, (await service.RefreshSession(" ")).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await service.RefreshSession("missing")).ErrorType);

        var refreshed = await service.RefreshSession(created.Value!);
        Assert.True(refreshed.IsSuccess);
        Assert.NotEqual(created.Value, refreshed.Value);
        Assert.Equal(ErrorType.Unauthorized, (await service.ValidateSession(created.Value!)).ErrorType);
        Assert.True((await service.ValidateSession(refreshed.Value!)).IsSuccess);
        Assert.Equal(ErrorType.Validation, (await service.DeleteSession(" ")).ErrorType);
        Assert.True((await service.DeleteSession(refreshed.Value!)).IsSuccess);
        Assert.Empty(store.Current!.SecurityContext.Sessions);
    }

    [Fact]
    public async Task Sessions_ExpiredTokensAreRejectedAndRemovedOnRefresh()
    {
        var expired = new Session("expired", Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-1));
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { Sessions = [expired] } });
        var service = new FuseUserSessionService(store);

        Assert.Equal(ErrorType.Unauthorized, (await service.ValidateSession(expired.Token)).ErrorType);
        Assert.Equal(ErrorType.Unauthorized, (await service.RefreshSession(expired.Token)).ErrorType);
        Assert.Empty(store.Current!.SecurityContext.Sessions);
    }
}
