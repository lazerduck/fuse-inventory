using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Fuse.Core.Interfaces;
using Fuse.Core.Areas.Security.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Fuse.API.Entra;
using Fuse.Core.Areas.Security.Entra;
using Fuse.Core.Areas.Security.Services;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using System.Security.Claims;
using Xunit;

namespace Fuse.Tests.Services;

public class EntraIdentityServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static FuseUser User(string name = "local") => new(Guid.NewGuid(), name, "hash", "salt", false, [], DateTime.UtcNow, DateTime.UtcNow);
    private static async Task<InMemoryFuseStore> Store()
    {
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            {
                Entra = new EntraSettings { Enabled = true, TenantId = Tenant },
                Users = [User("recovery") with { IsAdmin = true }]
            }
        });
        return store;
    }
    private static VerifiedEntraIdentity Identity(Guid? oid = null, params Guid[] groups) =>
        new(Tenant, oid ?? Guid.NewGuid(), "Someone", groups, DateTime.UtcNow.AddHours(1));

    [Fact]
    public async Task ConcurrentRecoveryAdminDeletionRetainsOneLocalAdministrator()
    {
        var store = await Store();
        var second = User("second-recovery") with { IsAdmin = true };
        await store.UpdateAsync(s => s with
        {
            SecurityContext = s.SecurityContext with
            { Users = s.SecurityContext.Users.Append(second).ToArray() }
        });
        var users = new FuseUserService(store, new FuseRoleService(store, []));
        var results = await Task.WhenAll(store.Current!.SecurityContext.Users.Select(u => Task.Run(() => users.DeleteUser(u.Id))));
        Assert.Single(results, r => r.IsSuccess);
        Assert.True(Assert.Single(store.Current.SecurityContext.Users).IsAdmin);
    }

    [Fact]
    public async Task OidcTicketUsesValidatedIdentityAfterProtocolClaimsAreRemoved()
    {
        var store = await Store();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFuseStore>(store);
        services.AddSingleton<IEntraIdentityService>(new EntraIdentityService(store));
        EntraAuthentication.Register(services);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(EntraAuthentication.Scheme);
        var http = new DefaultHttpContext { RequestServices = provider };
        var scheme = new AuthenticationScheme(EntraAuthentication.Scheme, null,
            typeof(OpenIdConnectHandler));
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("tid", Tenant.ToString()),
            new Claim("oid", Guid.NewGuid().ToString()),
            new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()),
            new Claim("name", "Verified Microsoft user")
        }, "Entra");
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties();
        properties.Items["configuration"] = store.Current!.SecurityContext.Entra.Version.ToString();
        // The OIDC handler has verified the signed token before invoking this event.
        await options.Events.TokenValidated(new TokenValidatedContext(http, scheme, options, principal, properties));
        identity.RemoveClaim(identity.FindFirst("exp"));
        var ticket = new AuthenticationTicket(principal, properties, scheme.Name);
        await options.Events.TicketReceived(new TicketReceivedContext(http, scheme, options, ticket));
        Assert.Equal("/?entra=complete", http.Response.Headers.Location.ToString());
        Assert.Contains(EntraAuthentication.HandoffCookie, http.Response.Headers.SetCookie.ToString());
        Assert.Single(store.Current.SecurityContext.Sessions);
    }

    [Fact]
    public async Task FirstSignInCreatesPasswordlessUserAndDoesNotCopyGroupRolesToDirectAssignments()
    {
        var store = await Store();
        var group = Guid.NewGuid();
        var role = new FuseRole(Guid.NewGuid(), "Editor", "", ["tags:read"], DateTime.UtcNow, DateTime.UtcNow)
        { EntraGroups = [new(group, "Engineering")] };
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { Roles = [role] } });
        var service = new EntraIdentityService(store);
        var session = (await service.SignIn(Identity(null, group), store.Current!.SecurityContext.Entra.Version)).Value!;
        var security = store.Current.SecurityContext;
        var user = security.Users.Single(u => u.Id == session.UserId);
        Assert.Empty(user.PasswordHash);
        Assert.Empty(user.RoleIds);
        Assert.False(user.IsAdmin);
        var membership = Assert.Single(RoleMembershipResolver.Resolve(security, user, session.EntraGroupIds));
        Assert.Equal(role.Id, membership.RoleId);
        Assert.False(membership.Direct);
        Assert.Equal(group, Assert.Single(membership.EntraGroups).GroupId);
        Assert.True((await new FuseUserSessionService(store).ValidateSession(session.Token)).IsSuccess);
        Assert.False((await new FuseUserSessionService(store).RefreshSession(session.Token)).IsSuccess);
    }

    [Fact]
    public async Task MultipleGrantSourcesRemainIndependentAndMappingRemovalTakesEffectImmediately()
    {
        var store = await Store();
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var role = new FuseRole(Guid.NewGuid(), "Editor", "", [], DateTime.UtcNow, DateTime.UtcNow)
        { EntraGroups = [new(g1, "One"), new(g2, "Two")] };
        var user = User() with { RoleIds = [role.Id] };
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { Roles = [role] } });
        var both = Assert.Single(RoleMembershipResolver.Resolve(store.Current!.SecurityContext, user, [g1, g2]));
        Assert.True(both.Direct);
        Assert.Equal(2, both.EntraGroups.Count);
        var inherited = Assert.Single(RoleMembershipResolver.Resolve(store.Current.SecurityContext, user with { RoleIds = [] }, [g1]));
        Assert.False(inherited.Direct);
        var direct = Assert.Single(RoleMembershipResolver.Resolve(store.Current.SecurityContext, user, []));
        Assert.True(direct.Direct);
        Assert.Empty(direct.EntraGroups);
        var unmapped = store.Current.SecurityContext with { Roles = [role with { EntraGroups = [] }] };
        Assert.Empty(RoleMembershipResolver.Resolve(unmapped, user with { RoleIds = [] }, [g1, g2]));
        Assert.Empty(RoleMembershipResolver.Resolve(store.Current.SecurityContext, user with { IsDisabled = true }, [g1]));
    }

    [Fact]
    public async Task RevokeRejectsOldSessionsAndClearsCachedGroupsButAllowsFreshSignIn()
    {
        var store = await Store();
        var service = new EntraIdentityService(store);
        var identity = Identity(null, Guid.NewGuid());
        var session = (await service.SignIn(identity, store.Current!.SecurityContext.Entra.Version)).Value!;
        Assert.True((await service.Revoke(session.UserId)).IsSuccess);
        Assert.False((await new FuseUserSessionService(store).ValidateSession(session.Token)).IsSuccess);
        Assert.Empty(store.Current.SecurityContext.Users.Single(u => u.Id == session.UserId).LastEntraGroupIds);
        var next = await service.SignIn(identity with { GroupIds = [] }, store.Current.SecurityContext.Entra.Version);
        Assert.True(next.IsSuccess);
        Assert.Empty(next.Value!.EntraGroupIds);
        Assert.Equal(1, next.Value.Version);
    }

    [Fact]
    public async Task DisabledUserCannotSignInAndLastRecoveryAdminCannotBeDisabled()
    {
        var store = await Store();
        var service = new EntraIdentityService(store);
        var identity = Identity();
        var session = (await service.SignIn(identity, store.Current!.SecurityContext.Entra.Version)).Value!;
        Assert.True((await service.Revoke(session.UserId, true)).IsSuccess);
        Assert.False((await service.SignIn(identity, store.Current.SecurityContext.Entra.Version)).IsSuccess);
        Assert.False((await service.Revoke(store.Current.SecurityContext.Users.Single(u => u.IsAdmin).Id, true)).IsSuccess);
    }

    [Fact]
    public async Task TenantConfigurationAndExpiryAreValidatedEvenAfterChallengeStarted()
    {
        var store = await Store();
        var service = new EntraIdentityService(store);
        var version = store.Current!.SecurityContext.Entra.Version;
        Assert.False((await service.SignIn(Identity() with { TenantId = Guid.NewGuid() }, version)).IsSuccess);
        Assert.False((await service.SignIn(Identity() with { ValidUntil = DateTime.UtcNow.AddMinutes(-1) }, version)).IsSuccess);
        Assert.False((await service.SignIn(Identity(), Guid.NewGuid())).IsSuccess);
        var session = (await service.SignIn(Identity(), version)).Value!;
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { Entra = s.SecurityContext.Entra with { Enabled = false } } });
        Assert.False((await new FuseUserSessionService(store).ValidateSession(session.Token)).IsSuccess);
    }

    [Fact]
    public async Task LinkingRequiresCurrentProofAndDoesNotMatchDisplayNamesOrStealAnIdentity()
    {
        var store = await Store();
        var service = new EntraIdentityService(store);
        var local = User("Someone");
        await store.UpdateAsync(s => s with { SecurityContext = s.SecurityContext with { Users = s.SecurityContext.Users.Append(local).ToArray() } });
        var version = store.Current!.SecurityContext.Entra.Version;
        var identity = Identity();
        var external = (await service.SignIn(identity, version)).Value!;
        Assert.NotEqual(local.Id, external.UserId);
        Assert.False((await service.SignIn(identity, version, local.Id, 0)).IsSuccess);
        Assert.False((await service.SignIn(Identity(), version, local.Id, 1)).IsSuccess);
        var linked = (await service.SignIn(Identity(), version, local.Id, 0)).Value!;
        Assert.Equal(local.Id, linked.UserId);
    }

    [Fact]
    public async Task ConcurrentSignInsDoNotCreateDuplicateUsers()
    {
        var store = await Store();
        var identity = Identity();
        var service = new EntraIdentityService(store);
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => service.SignIn(identity, store.Current!.SecurityContext.Entra.Version)));
        Assert.Single(results.Select(r => r.Value!.UserId).Distinct());
        Assert.Equal(2, store.Current!.SecurityContext.Users.Count);
    }

    [Fact]
    public void ClaimsRejectOverageWrongTenantAndMalformedGroups()
    {
        ClaimsPrincipal Principal(params Claim[] extra) => new(new ClaimsIdentity(new[] {
            new Claim("tid", Tenant.ToString()),
            new Claim("oid", Guid.NewGuid().ToString()),
            new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()) }.Concat(extra)));
        Assert.True(EntraAuthentication.ReadIdentity(Principal(), Tenant).IsSuccess);
        Assert.False(EntraAuthentication.ReadIdentity(Principal(), Guid.NewGuid()).IsSuccess);
        Assert.False(EntraAuthentication.ReadIdentity(Principal(new Claim("hasgroups", "true")), Tenant).IsSuccess);
        Assert.False(EntraAuthentication.ReadIdentity(Principal(new Claim("_claim_names", "{\"groups\":\"src1\"}")), Tenant).IsSuccess);
        Assert.False(EntraAuthentication.ReadIdentity(Principal(new Claim("groups", "not-a-guid")), Tenant).IsSuccess);
    }

    [Fact]
    public void HandoffTicketsAreSingleUseAndCannotBeGuessed()
    {
        var tickets = new EntraFlowTickets();
        var ticket = new EntraFlowTicket("secret-session", null, null);
        var key = tickets.Issue(ticket);
        Assert.Null(tickets.Take("invalid"));
        Assert.Equal(ticket, tickets.Take(key));
        Assert.Null(tickets.Take(key));
    }
    [Fact]
    public async Task SnapshotValidationAcceptsOnlyCompleteUniqueExternalIdentities()
    {
        var store = await Store();
        var oid = Guid.NewGuid();
        var external = User() with { PasswordHash = "", PasswordSalt = "", EntraTenantId = Tenant, EntraObjectId = oid };
        var snapshot = store.Current! with { SecurityContext = store.Current!.SecurityContext with { Users = [store.Current.SecurityContext.Users[0], external] } };
        Assert.Empty(Fuse.Core.Helpers.SnapshotValidator.Validate(snapshot));
        Assert.NotEmpty(Fuse.Core.Helpers.SnapshotValidator.Validate(snapshot with { SecurityContext = snapshot.SecurityContext with { Users = [external with { EntraObjectId = null }] } }));
        Assert.NotEmpty(Fuse.Core.Helpers.SnapshotValidator.Validate(snapshot with { SecurityContext = snapshot.SecurityContext with { Users = [external, external with { Id = Guid.NewGuid(), UserName = "duplicate identity" }] } }));
    }
}
