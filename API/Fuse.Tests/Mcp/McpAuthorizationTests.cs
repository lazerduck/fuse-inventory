using System.Security.Claims;
using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Tag;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.MCP;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using Moq;
using Xunit;

namespace Fuse.Tests.Mcp;

public class McpAuthorizationTests
{
    [Fact]
    public async Task RequireAsync_RejectsMissingAuthentication()
    {
        var authorization = CreateAuthorization(new ClaimsPrincipal(new ClaimsIdentity()), Mock.Of<IFuseRoleService>());

        var exception = await Assert.ThrowsAsync<McpException>(() => authorization.RequireAsync(TagPermissions.ReadKey, default));

        Assert.Equal("Authentication is required.", exception.Message);
    }

    [Fact]
    public async Task RequireAsync_AllowsAdminsWithoutLoadingRoles()
    {
        var roles = new Mock<IFuseRoleService>();
        var user = Authenticated(new Claim(FuseAuthenticationClaims.IsAdmin, bool.TrueString));
        var authorization = CreateAuthorization(user, roles.Object, []);

        await authorization.RequireAsync("even:an-unknown-permission", default);

        roles.Verify(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task RequireAsync_AllowsPermissionFromAnyAssignedRole_CaseInsensitively()
    {
        var roleId = Guid.NewGuid();
        var invalidRoleClaim = new Claim(FuseAuthenticationClaims.RoleId, "not-a-guid");
        var user = Authenticated(invalidRoleClaim, new Claim(FuseAuthenticationClaims.RoleId, roleId.ToString()));
        var role = new FuseRole(roleId, "Reader", "", [TagPermissions.ReadKey.ToUpperInvariant()], DateTime.UtcNow, DateTime.UtcNow);
        var roles = new Mock<IFuseRoleService>();
        roles.Setup(x => x.GetRolesByIds(It.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { roleId }))))
            .ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success([role]));
        var authorization = CreateAuthorization(user, roles.Object);

        await authorization.RequireAsync(TagPermissions.ReadKey, default);

        roles.VerifyAll();
    }

    [Fact]
    public async Task RequireAsync_RejectsUnknownConfiguredPermissionBeforeLoadingRoles()
    {
        var roles = new Mock<IFuseRoleService>();
        var user = Authenticated(new Claim(FuseAuthenticationClaims.RoleId, Guid.NewGuid().ToString()));
        var authorization = CreateAuthorization(user, roles.Object);

        var exception = await Assert.ThrowsAsync<McpException>(() => authorization.RequireAsync("unknown:key", default));

        Assert.Contains("unknown permission", exception.Message);
        roles.Verify(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequireAsync_RejectsMissingRoleClaimsOrRolesWithoutPermission(bool includeRoleClaim)
    {
        var roleId = Guid.NewGuid();
        var claims = includeRoleClaim ? new[] { new Claim(FuseAuthenticationClaims.RoleId, roleId.ToString()) } : [];
        var roles = new Mock<IFuseRoleService>();
        roles.Setup(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()))
            .ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Failure("roles missing", ErrorType.NotFound));
        var authorization = CreateAuthorization(Authenticated(claims), roles.Object);

        var exception = await Assert.ThrowsAsync<McpException>(() => authorization.RequireAsync(TagPermissions.ReadKey, default));

        Assert.Contains(TagPermissions.ReadKey, exception.Message);
        if (!includeRoleClaim)
            roles.Verify(x => x.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    private static McpToolAuthorization CreateAuthorization(
        ClaimsPrincipal user,
        IFuseRoleService roles,
        IEnumerable<AreaPermissions>? catalogs = null)
    {
        var context = new DefaultHttpContext { User = user };
        return new McpToolAuthorization(
            new HttpContextAccessor { HttpContext = context },
            roles,
            catalogs ?? [new TagPermissions()]);
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));
}
