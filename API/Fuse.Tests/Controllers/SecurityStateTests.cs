using System.Security.Claims;
using Fuse.API.Controllers;
using Fuse.API.Middleware;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Fuse.Tests.Controllers;

public class SecurityStateTests
{
    private readonly Mock<IFuseUserService> users = new();
    private readonly Mock<IFuseRoleService> roles = new();

    private SecurityController Controller(Guid? userId = null)
    {
        var security = new Mock<IFuseSecurityService>();
        security.Setup(s => s.GetSecurityPosture()).ReturnsAsync(SecurityPosture.FullyRestricted);
        var controller = new SecurityController(security.Object, users.Object,
            Mock.Of<IFuseUserSessionService>(), roles.Object);
        var context = new DefaultHttpContext();
        if (userId is not null)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString()!)], AuthenticationMiddleware.UserAuthType));
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private static FuseUser User(params Guid[] roleIds) => new(Guid.NewGuid(), "Reader", "hash", "salt",
        false, roleIds, DateTime.UtcNow, DateTime.UtcNow);

    private static SecurityController.SecurityStateResponse State(ActionResult<SecurityController.SecurityStateResponse> result)
        => Assert.IsType<SecurityController.SecurityStateResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public async Task AnonymousStateDoesNotLoadRolesOrExposePermissions()
    {
        var state = State(await Controller().GetState());
        Assert.Null(state.CurrentUser);
        Assert.Empty(state.Permissions);
        users.Verify(s => s.GetUser(It.IsAny<Guid>()), Times.Never);
        roles.Verify(s => s.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task StateReturnsOnlyAssignedPermissionsWithoutRequiringRoleRead()
    {
        var roleId = Guid.NewGuid();
        var otherRoleId = Guid.NewGuid();
        var user = User(roleId, otherRoleId);
        users.Setup(s => s.GetUser(user.Id)).ReturnsAsync(Result<FuseUser>.Success(user));
        roles.Setup(s => s.GetRolesByIds(user.RoleIds)).ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success([
            new(roleId, "Reader", "", ["tags:read"], DateTime.UtcNow, DateTime.UtcNow),
            new(otherRoleId, "Creator", "", ["tags:read", "tags:create"], DateTime.UtcNow, DateTime.UtcNow)
        ]));
        var state = State(await Controller(user.Id).GetState());
        Assert.Equal(user.Id, state.CurrentUser!.Id);
        Assert.Equal(["tags:read", "tags:create"], state.Permissions);
        roles.Verify(s => s.GetRolesByIds(user.RoleIds), Times.Once);
        roles.Verify(s => s.GetRoles(), Times.Never);
    }

    [Fact]
    public async Task FailedRoleResolutionReturnsNoPermissions()
    {
        var user = User(Guid.NewGuid());
        users.Setup(s => s.GetUser(user.Id)).ReturnsAsync(Result<FuseUser>.Success(user));
        roles.Setup(s => s.GetRolesByIds(user.RoleIds))
            .ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Failure("Missing role", ErrorType.NotFound));
        Assert.Empty(State(await Controller(user.Id).GetState()).Permissions);
    }

    [Fact]
    public async Task DeletedUserDoesNotExposePermissionsFromOldClaims()
    {
        var userId = Guid.NewGuid();
        users.Setup(s => s.GetUser(userId)).ReturnsAsync(Result<FuseUser>.Failure("Missing user", ErrorType.NotFound));
        var state = State(await Controller(userId).GetState());
        Assert.Null(state.CurrentUser);
        Assert.Empty(state.Permissions);
        roles.Verify(s => s.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }
}
