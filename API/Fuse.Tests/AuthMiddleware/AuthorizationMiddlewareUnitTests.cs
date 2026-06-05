using System.Security.Claims;
using Fuse.API;
using Fuse.API.Middleware;
using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Fuse.Tests.AuthMiddleware;

public class AuthorizationMiddlewareUnitTests
{
    [Fact]
    public async Task InvokeAsync_AdminUser_BypassesAuthorizationChecks()
    {
        var wasNextCalled = false;
        var middleware = new AuthorizationMiddleware(_ =>
        {
            wasNextCalled = true;
            return Task.CompletedTask;
        });

        var context = NewContext(authenticated: true, isAdmin: true);
        var store = new Mock<IFuseStore>(MockBehavior.Strict);

        await middleware.InvokeAsync(context, store.Object, Mock.Of<IFuseRoleService>(), Array.Empty<AreaPermissions>());

        Assert.True(wasNextCalled);
    }

    [Fact]
    public async Task InvokeAsync_RequiresSetup_AllowedEndpoint_AllowsRequest()
    {
        var wasNextCalled = false;
        var middleware = new AuthorizationMiddleware(_ =>
        {
            wasNextCalled = true;
            return Task.CompletedTask;
        });

        var context = NewContext(authenticated: false, allowDuringSetup: true);
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.FullyRestricted,
            users: Array.Empty<FuseUser>()));

        await middleware.InvokeAsync(context, store.Object, Mock.Of<IFuseRoleService>(), Array.Empty<AreaPermissions>());

        Assert.True(wasNextCalled);
    }

    [Fact]
    public async Task InvokeAsync_RequiresSetup_UnauthenticatedNonSetupEndpoint_Returns401()
    {
        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask);
        var context = NewContext(authenticated: false, requiredPermissionKey: "accounts:read");
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.FullyRestricted,
            users: Array.Empty<FuseUser>()));

        await middleware.InvokeAsync(context, store.Object, Mock.Of<IFuseRoleService>(), Array.Empty<AreaPermissions>());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_RequiresSetup_AuthenticatedNonSetupEndpoint_Returns403()
    {
        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask);
        var context = NewContext(authenticated: true, requiredPermissionKey: "accounts:read");
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.FullyRestricted,
            users: Array.Empty<FuseUser>()));

        await middleware.InvokeAsync(context, store.Object, Mock.Of<IFuseRoleService>(), Array.Empty<AreaPermissions>());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Unrestricted_WithRegularPermission_AllowsWithoutRoleCheck()
    {
        var wasNextCalled = false;
        var middleware = new AuthorizationMiddleware(_ =>
        {
            wasNextCalled = true;
            return Task.CompletedTask;
        });

        var requiredKey = "accounts:read";
        var context = NewContext(authenticated: false, requiredPermissionKey: requiredKey);
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.Unrestricted,
            users: new[] { NewAdminUser() }));

        var roleService = new Mock<IFuseRoleService>(MockBehavior.Strict);
        var catalogs = new[]
        {
            new TestPermissions(requiredKey, isAllowedInRestrictedEditing: false, ignorePosture: false)
        };

        await middleware.InvokeAsync(context, store.Object, roleService.Object, catalogs);

        Assert.True(wasNextCalled);
    }

    [Fact]
    public async Task InvokeAsync_Unrestricted_WithIgnorePosturePermission_UnauthenticatedReturns401()
    {
        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask);
        var requiredKey = "security:write";
        var context = NewContext(authenticated: false, requiredPermissionKey: requiredKey);
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.Unrestricted,
            users: new[] { NewAdminUser() }));

        var catalogs = new[]
        {
            new TestPermissions(requiredKey, isAllowedInRestrictedEditing: false, ignorePosture: true)
        };

        await middleware.InvokeAsync(context, store.Object, Mock.Of<IFuseRoleService>(), catalogs);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_RestrictedEditing_WithAllowedDescriptor_Allows()
    {
        var wasNextCalled = false;
        var middleware = new AuthorizationMiddleware(_ =>
        {
            wasNextCalled = true;
            return Task.CompletedTask;
        });

        var requiredKey = "applications:read";
        var context = NewContext(authenticated: false, requiredPermissionKey: requiredKey);
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.RestrictedEditing,
            users: new[] { NewAdminUser() }));

        var catalogs = new[]
        {
            new TestPermissions(requiredKey, isAllowedInRestrictedEditing: true, ignorePosture: false)
        };

        await middleware.InvokeAsync(context, store.Object, Mock.Of<IFuseRoleService>(), catalogs);

        Assert.True(wasNextCalled);
    }

    [Fact]
    public async Task InvokeAsync_RestrictedEditing_WithoutRequiredPermission_Allows()
    {
        var wasNextCalled = false;
        var middleware = new AuthorizationMiddleware(_ =>
        {
            wasNextCalled = true;
            return Task.CompletedTask;
        });

        var context = NewContext(authenticated: false, requiredPermissionKey: null);
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.RestrictedEditing,
            users: new[] { NewAdminUser() }));

        await middleware.InvokeAsync(context, store.Object, Mock.Of<IFuseRoleService>(), Array.Empty<AreaPermissions>());

        Assert.True(wasNextCalled);
    }

    [Fact]
    public async Task InvokeAsync_RestrictedEditing_NotAllowedByPostureButUserHasPermission_Allows()
    {
        var wasNextCalled = false;
        var middleware = new AuthorizationMiddleware(_ =>
        {
            wasNextCalled = true;
            return Task.CompletedTask;
        });

        var requiredKey = "accounts:write";
        var roleId = Guid.NewGuid();
        var context = NewContext(authenticated: true, requiredPermissionKey: requiredKey, roleIds: new[] { roleId });
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.RestrictedEditing,
            users: new[] { NewAdminUser() }));

        var roleService = new Mock<IFuseRoleService>();
        roleService
            .Setup(s => s.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()))
            .ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success(new[]
            {
                new FuseRole(roleId, "Writer", "", new[] { requiredKey }, DateTime.UtcNow, DateTime.UtcNow)
            }));

        var catalogs = new[]
        {
            new TestPermissions(requiredKey, isAllowedInRestrictedEditing: false, ignorePosture: false)
        };

        await middleware.InvokeAsync(context, store.Object, roleService.Object, catalogs);

        Assert.True(wasNextCalled);
    }

    [Fact]
    public async Task InvokeAsync_FullyRestricted_AuthenticatedWithoutRoles_Returns403()
    {
        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask);
        var requiredKey = "accounts:read";
        var context = NewContext(authenticated: true, requiredPermissionKey: requiredKey);
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.FullyRestricted,
            users: new[] { NewAdminUser() }));

        await middleware.InvokeAsync(context, store.Object, Mock.Of<IFuseRoleService>(), Array.Empty<AreaPermissions>());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_FullyRestricted_WithRolePermission_Allows()
    {
        var wasNextCalled = false;
        var middleware = new AuthorizationMiddleware(_ =>
        {
            wasNextCalled = true;
            return Task.CompletedTask;
        });

        var requiredKey = "accounts:read";
        var roleId = Guid.NewGuid();
        var context = NewContext(authenticated: true, requiredPermissionKey: requiredKey, roleIds: new[] { roleId });
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.FullyRestricted,
            users: new[] { NewAdminUser() }));

        var roleService = new Mock<IFuseRoleService>();
        roleService
            .Setup(s => s.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()))
            .ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Success(new[]
            {
                new FuseRole(roleId, "Reader", "", new[] { requiredKey }, DateTime.UtcNow, DateTime.UtcNow)
            }));

        await middleware.InvokeAsync(context, store.Object, roleService.Object, Array.Empty<AreaPermissions>());

        Assert.True(wasNextCalled);
    }

    [Fact]
    public async Task InvokeAsync_FullyRestricted_WithoutRequiredPermission_Allows()
    {
        var wasNextCalled = false;
        var middleware = new AuthorizationMiddleware(_ =>
        {
            wasNextCalled = true;
            return Task.CompletedTask;
        });

        var context = NewContext(authenticated: true, requiredPermissionKey: null);
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.FullyRestricted,
            users: new[] { NewAdminUser() }));

        await middleware.InvokeAsync(context, store.Object, Mock.Of<IFuseRoleService>(), Array.Empty<AreaPermissions>());

        Assert.True(wasNextCalled);
    }

    [Fact]
    public async Task InvokeAsync_FullyRestricted_RoleLookupFails_Returns403()
    {
        var middleware = new AuthorizationMiddleware(_ => Task.CompletedTask);
        var requiredKey = "accounts:read";
        var roleId = Guid.NewGuid();
        var context = NewContext(authenticated: true, requiredPermissionKey: requiredKey, roleIds: new[] { roleId });
        var store = MockStore(NewSnapshot(
            posture: SecurityPosture.FullyRestricted,
            users: new[] { NewAdminUser() }));

        var roleService = new Mock<IFuseRoleService>();
        roleService
            .Setup(s => s.GetRolesByIds(It.IsAny<IReadOnlyList<Guid>>()))
            .ReturnsAsync(Result<IReadOnlyList<FuseRole>>.Failure("db down", ErrorType.ServerError));

        await middleware.InvokeAsync(context, store.Object, roleService.Object, Array.Empty<AreaPermissions>());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private static Mock<IFuseStore> MockStore(Snapshot snapshot)
    {
        var store = new Mock<IFuseStore>();
        store.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        return store;
    }

    private static DefaultHttpContext NewContext(
        bool authenticated,
        bool isAdmin = false,
        string? requiredPermissionKey = null,
        IReadOnlyList<Guid>? roleIds = null,
        bool allowDuringSetup = false)
    {
        var claims = new List<Claim>();
        if (authenticated)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
            if (isAdmin)
                claims.Add(new Claim(AuthenticationMiddleware.IsAdminClaimType, bool.TrueString));

            if (roleIds is not null)
            {
                foreach (var roleId in roleIds)
                    claims.Add(new Claim(AuthenticationMiddleware.RoleIdClaimType, roleId.ToString()));
            }
        }

        var identity = authenticated
            ? new ClaimsIdentity(claims, "test")
            : new ClaimsIdentity();

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var metadataItems = new List<object>();
        if (requiredPermissionKey is not null)
            metadataItems.Add(new RequirePermissionKeyAttribute(requiredPermissionKey));
        if (allowDuringSetup)
            metadataItems.Add(new AllowDuringSetupAttribute());

        var metadata = metadataItems.Count == 0
            ? EndpointMetadataCollection.Empty
            : new EndpointMetadataCollection(metadataItems.ToArray());
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test-endpoint"));

        return context;
    }

    private static Snapshot NewSnapshot(SecurityPosture posture, IReadOnlyList<FuseUser> users)
        => new(
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
            Risks: Array.Empty<Risk>(),
            MessageBrokers: Array.Empty<MessageBroker>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.None, DateTime.UtcNow), Array.Empty<SecurityUser>()),
            SecurityContext: new SecurityContext(posture, Array.Empty<FuseRole>(), users, Array.Empty<FuseApiKey>(), Array.Empty<Session>()),
            AppSettings: new AppSettings()
        );

    private static FuseUser NewAdminUser()
        => new(
            Guid.NewGuid(),
            "admin",
            "hash",
            "salt",
            true,
            Array.Empty<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

    private sealed class TestPermissions : AreaPermissions
    {
        private readonly PermissionDescriptor _descriptor;

        public TestPermissions(string key, bool isAllowedInRestrictedEditing, bool ignorePosture)
        {
            _descriptor = new PermissionDescriptor(key, isAllowedInRestrictedEditing, ignorePosture);
        }

        public override string AreaName => "Test";

        public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors()
            => new[] { _descriptor };
    }
}