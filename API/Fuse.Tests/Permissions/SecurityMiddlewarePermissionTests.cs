using Fuse.API.Middleware;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Fuse.Tests.Permissions;

public class SecurityMiddlewarePermissionTests
{
    [Fact]
    public async Task InvokeAsync_RolePermission_AllowsRolesReadEndpoint()
    {
        var role = new Role(Guid.NewGuid(), "RoleReader", "", new[] { Permission.RolesRead }, DateTime.UtcNow, DateTime.UtcNow);
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var user = new SecurityUser(Guid.NewGuid(), "user", "hash", "salt", SecurityRole.Reader, new[] { role.Id }, DateTime.UtcNow, DateTime.UtcNow);

        var state = new SecurityState(
            new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow),
            new[] { admin, user },
            new[] { role }
        );

        var securityService = new Mock<ISecurityService>();
        securityService
            .Setup(s => s.GetSecurityStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        securityService
            .Setup(s => s.ValidateSessionAsync("token", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var permissionService = new PermissionService(securityService.Object);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/security/roles";
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.Authorization = "Bearer token";

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new SecurityMiddleware(next);

        await middleware.InvokeAsync(context, securityService.Object, permissionService);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_MissingPermission_ReturnsForbidden()
    {
        var role = new Role(Guid.NewGuid(), "RoleReader", "", new[] { Permission.RolesRead }, DateTime.UtcNow, DateTime.UtcNow);
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var user = new SecurityUser(Guid.NewGuid(), "user", "hash", "salt", SecurityRole.Reader, new[] { role.Id }, DateTime.UtcNow, DateTime.UtcNow);

        var state = new SecurityState(
            new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow),
            new[] { admin, user },
            new[] { role }
        );

        var securityService = new Mock<ISecurityService>();
        securityService
            .Setup(s => s.GetSecurityStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        securityService
            .Setup(s => s.ValidateSessionAsync("token", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var permissionService = new PermissionService(securityService.Object);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/security/roles";
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers.Authorization = "Bearer token";

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new SecurityMiddleware(next);

        await middleware.InvokeAsync(context, securityService.Object, permissionService);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AuditEndpoint_RequiresPermission()
    {
        var role = new Role(Guid.NewGuid(), "Audit", "", new[] { Permission.AuditLogsView }, DateTime.UtcNow, DateTime.UtcNow);
        var admin = new SecurityUser(Guid.NewGuid(), "admin", "hash", "salt", SecurityRole.Admin, DateTime.UtcNow, DateTime.UtcNow);
        var user = new SecurityUser(Guid.NewGuid(), "user", "hash", "salt", SecurityRole.Reader, new[] { role.Id }, DateTime.UtcNow, DateTime.UtcNow);

        var state = new SecurityState(
            new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow),
            new[] { admin, user },
            new[] { role }
        );

        var securityService = new Mock<ISecurityService>();
        securityService
            .Setup(s => s.GetSecurityStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        securityService
            .Setup(s => s.ValidateSessionAsync("token", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var permissionService = new PermissionService(securityService.Object);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/audit";
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.Authorization = "Bearer token";

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new SecurityMiddleware(next);

        await middleware.InvokeAsync(context, securityService.Object, permissionService);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }
}
