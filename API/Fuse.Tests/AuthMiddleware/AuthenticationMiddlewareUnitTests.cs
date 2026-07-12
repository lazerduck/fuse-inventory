using System.Security.Claims;
using Fuse.API.Middleware;
using Fuse.Core.Areas.Logging;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Fuse.Tests.AuthMiddleware;

public class AuthenticationMiddlewareUnitTests
{
    private static readonly ILogService LogService = Mock.Of<ILogService>();

    [Fact]
    public async Task InvokeAsync_WithValidApiKey_SetsApiKeyPrincipal()
    {
        var middleware = new AuthenticationMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers["x-api-key"] = "key-123";

        var roleId = Guid.NewGuid();
        var apiKeyService = new Mock<IFuseAPIKeyService>();
        apiKeyService
            .Setup(s => s.VerifyAPIKeys("key-123"))
            .ReturnsAsync(Result<FuseApiKey>.Success(new FuseApiKey(
                Guid.NewGuid(),
                "integration-key",
                "pref",
                "hash",
                "salt",
                Guid.NewGuid(),
                new[] { roleId },
                DateTime.UtcNow,
                DateTime.UtcNow)));

        var sessionService = new Mock<IFuseUserSessionService>();
        var userService = new Mock<IFuseUserService>();

        await middleware.InvokeAsync(context, apiKeyService.Object, sessionService.Object, userService.Object, LogService);

        Assert.Equal(AuthenticationMiddleware.ApiKeyAuthType, context.User.Identity?.AuthenticationType);
        Assert.Equal("integration-key", context.User.FindFirstValue(ClaimTypes.Name));
        Assert.Equal(roleId.ToString(), context.User.FindFirstValue(AuthenticationMiddleware.RoleIdClaimType));
        sessionService.Verify(s => s.ValidateSession(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidApiKey_DoesNotSetUser()
    {
        var middleware = new AuthenticationMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers["x-api-key"] = "bad-key";

        var apiKeyService = new Mock<IFuseAPIKeyService>();
        apiKeyService
            .Setup(s => s.VerifyAPIKeys("bad-key"))
            .ReturnsAsync(Result<FuseApiKey>.Failure("invalid", ErrorType.Unauthorized));

        await middleware.InvokeAsync(
            context,
            apiKeyService.Object,
            Mock.Of<IFuseUserSessionService>(),
            Mock.Of<IFuseUserService>(),
            LogService);

        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
    }

    [Fact]
    public async Task InvokeAsync_WithBearerToken_AuthenticatesUserAndAddsAdminClaim()
    {
        var middleware = new AuthenticationMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer token-1";

        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sessionService = new Mock<IFuseUserSessionService>();
        sessionService
            .Setup(s => s.ValidateSession("token-1"))
            .ReturnsAsync(Result<Guid>.Success(userId));

        var userService = new Mock<IFuseUserService>();
        userService
            .Setup(s => s.GetUser(userId))
            .ReturnsAsync(Result<FuseUser>.Success(new FuseUser(
                userId,
                "admin-user",
                "hash",
                "salt",
                true,
                new[] { roleId },
                DateTime.UtcNow,
                DateTime.UtcNow)));

        await middleware.InvokeAsync(context, Mock.Of<IFuseAPIKeyService>(), sessionService.Object, userService.Object, LogService);

        Assert.Equal(AuthenticationMiddleware.UserAuthType, context.User.Identity?.AuthenticationType);
        Assert.Equal("admin-user", context.User.FindFirstValue(ClaimTypes.Name));
        Assert.Equal(bool.TrueString, context.User.FindFirstValue(AuthenticationMiddleware.IsAdminClaimType));
        Assert.Equal(roleId.ToString(), context.User.FindFirstValue(AuthenticationMiddleware.RoleIdClaimType));
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidBearerPrefix_DoesNotCallSessionService()
    {
        var middleware = new AuthenticationMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Token abc";

        var sessionService = new Mock<IFuseUserSessionService>();

        await middleware.InvokeAsync(context, Mock.Of<IFuseAPIKeyService>(), sessionService.Object, Mock.Of<IFuseUserService>(), LogService);

        sessionService.Verify(s => s.ValidateSession(It.IsAny<string>()), Times.Never);
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyApiKey_UsesBearerFlow()
    {
        var middleware = new AuthenticationMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers["x-api-key"] = "   ";
        context.Request.Headers.Authorization = "Bearer token-2";

        var userId = Guid.NewGuid();
        var sessionService = new Mock<IFuseUserSessionService>();
        sessionService
            .Setup(s => s.ValidateSession("token-2"))
            .ReturnsAsync(Result<Guid>.Success(userId));

        var userService = new Mock<IFuseUserService>();
        userService
            .Setup(s => s.GetUser(userId))
            .ReturnsAsync(Result<FuseUser>.Success(new FuseUser(
                userId,
                "normal-user",
                "hash",
                "salt",
                false,
                Array.Empty<Guid>(),
                DateTime.UtcNow,
                DateTime.UtcNow)));

        await middleware.InvokeAsync(context, Mock.Of<IFuseAPIKeyService>(), sessionService.Object, userService.Object, LogService);

        Assert.Equal(AuthenticationMiddleware.UserAuthType, context.User.Identity?.AuthenticationType);
        Assert.Equal("normal-user", context.User.FindFirstValue(ClaimTypes.Name));
    }
}