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
    public static IEnumerable<object[]> SecurityEndpointCases()
    {
        yield return new object[] { "/api/security/accounts", HttpMethods.Get, Permission.UsersRead };
        yield return new object[] { "/api/security/accounts", HttpMethods.Post, Permission.UsersCreate };
        yield return new object[] { "/api/security/accounts", HttpMethods.Patch, Permission.UsersUpdate };
        yield return new object[] { "/api/security/accounts", HttpMethods.Put, Permission.UsersUpdate };
        yield return new object[] { "/api/security/accounts", HttpMethods.Delete, Permission.UsersDelete };

        yield return new object[] { "/api/security/roles", HttpMethods.Get, Permission.RolesRead };
        yield return new object[] { "/api/security/roles", HttpMethods.Post, Permission.RolesCreate };
        yield return new object[] { "/api/security/roles", HttpMethods.Patch, Permission.RolesUpdate };
        yield return new object[] { "/api/security/roles", HttpMethods.Put, Permission.RolesUpdate };
        yield return new object[] { "/api/security/roles", HttpMethods.Delete, Permission.RolesDelete };

        yield return new object[] { "/api/roles", HttpMethods.Get, Permission.RolesRead };
        yield return new object[] { "/api/roles", HttpMethods.Post, Permission.RolesCreate };
        yield return new object[] { "/api/roles", HttpMethods.Patch, Permission.RolesUpdate };
        yield return new object[] { "/api/roles", HttpMethods.Put, Permission.RolesUpdate };
        yield return new object[] { "/api/roles", HttpMethods.Delete, Permission.RolesDelete };
    }

    public static IEnumerable<object[]> SpecialEndpointCases()
    {
        yield return new object[] { "/api/audit", HttpMethods.Get, Permission.AuditLogsView };
        yield return new object[] { "/api/config/export", HttpMethods.Get, Permission.ConfigurationExport };
    }

    [Theory]
    [MemberData(nameof(SecurityEndpointCases))]
    public async Task InvokeAsync_SecurityEndpoints_RequirePermissions(string path, string method, Permission required)
    {
        var role = new Role(Guid.NewGuid(), "Role", "", new[] { required }, DateTime.UtcNow, DateTime.UtcNow);
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
        context.Request.Path = path;
        context.Request.Method = method;
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

    [Theory]
    [MemberData(nameof(SecurityEndpointCases))]
    public async Task InvokeAsync_SecurityEndpoints_MissingPermission_ReturnsForbidden(string path, string method, Permission required)
    {
        // Give user a different permission than what's required for this endpoint
        var wrongPermission = required == Permission.RolesRead ? Permission.UsersRead : Permission.ApplicationsRead;
        var role = new Role(Guid.NewGuid(), "Role", "", new[] { wrongPermission }, DateTime.UtcNow, DateTime.UtcNow);
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
        context.Request.Path = path;
        context.Request.Method = method;
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

    [Theory]
    [MemberData(nameof(SpecialEndpointCases))]
    public async Task InvokeAsync_SpecialEndpoints_RequirePermissions(string path, string method, Permission required)
    {
        var role = new Role(Guid.NewGuid(), "Role", "", new[] { required }, DateTime.UtcNow, DateTime.UtcNow);
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
        context.Request.Path = path;
        context.Request.Method = method;
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
