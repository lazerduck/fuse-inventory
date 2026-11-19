using System.Security.Claims;
using System.Text.Json;
using Fuse.API.Controllers;
using Fuse.API.Middleware;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Areas.RedisIntegration;
using Fuse.Core.Responses;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class IntegrationPasswordTests
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private const string Pinned = "0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(null)]
    [InlineData(Pinned)]
    public async Task PasswordResolvedAtUseTimeNeverPersistedRepointedOrReplacedByFallback(string? version)
    {
        var store = new InMemoryFuseStore();
        var vault = new SecretProvider(Guid.NewGuid(), "keys", new("https://test.vault.azure.net/"),
            SecretProviderAuthMode.ManagedIdentity, null, SecretProviderCapabilities.Read, Now, Now);
        var conn = new MongoDbIntegration(Guid.NewGuid(), "catalog", Guid.NewGuid(), new("mongodb://mongo.test/lab"),
            "reader", "legacy-password", Now, Now, "admin", true);
        await store.UpdateAsync(s => s with { SecretProviders = [vault], MongoDbIntegrations = [conn] });
        var audit = new FakeAuditService();
        var azure = new Mock<IAzureKeyVaultClient>(MockBehavior.Strict);
        var value = "fresh-password-one";
        var fail = false;
        azure.Setup(v => v.ReadSecretAsync(vault, "inventory", version)).ReturnsAsync(() =>
            fail ? Result<string>.Failure("sensitive upstream diagnostic") : Result<string>.Success(version is null ? value : "fixed-version-password"));
        var passwords = new IntegrationPasswords(store, azure.Object, audit, Mock.Of<ICurrentUser>());
        var reference = new AzureKeyVaultBinding(vault.Id, "inventory", version);
        Assert.Equal("legacy-password", (await IntegrationPasswords.ForUse(conn, null, default)).Value!.Password);
        var bound = await passwords.BindAsync("mongodb", conn.Id, new(IntegrationPasswords.View(conn).Revision, reference), _ => true, default);
        Assert.True(bound.IsSuccess);
        var saved = (await store.GetAsync()).MongoDbIntegrations.Single();
        Assert.Null(saved.Password);
        Assert.Equal(version is null ? value : "fixed-version-password", (await IntegrationPasswords.ForUse(saved, passwords, default)).Value!.Password);
        value = "fresh-password-two";
        Assert.Equal(version is null ? value : "fixed-version-password", (await IntegrationPasswords.ForUse(saved, passwords, default)).Value!.Password);
        fail = true;
        Assert.Equal(IntegrationPasswords.Unavailable, (await IntegrationPasswords.ForUse(saved, passwords, default)).Error);
        foreach (var redirected in new[] { saved with { Endpoint = new("mongodb://other.test/lab") }, saved with { UseTls = false },
            saved with { Username = "different" }, saved with { AuthenticationDatabase = "other" }, saved with { DataStoreId = Guid.NewGuid() } })
        {
            Assert.True(IntegrationPasswords.InvalidEdit(saved, redirected, null));
            Assert.False((await IntegrationPasswords.ForUse(redirected, passwords, default)).IsSuccess);
        }
        Assert.False(IntegrationPasswords.InvalidEdit(saved, saved with { Name = "New label" }, null));
        Assert.True(IntegrationPasswords.InvalidEdit(saved, saved, "another"));
        azure.Verify(v => v.ReadSecretAsync(vault, "inventory", version), Times.Exactly(4));
        var auditAndState = JsonSerializer.Serialize(new { audit.Logs, Saved = saved, Response = bound.Value });
        foreach (var secret in new[] { "legacy-password", "fresh-password-one", value, "fixed-version-password" })
            Assert.DoesNotContain(secret, auditAndState);
        Assert.Equal(ErrorType.Conflict, (await passwords.BindAsync("mongodb", conn.Id,
            new(IntegrationPasswords.View(conn).Revision, null), _ => true, default)).ErrorType);
        Assert.True((await passwords.BindAsync("mongodb", conn.Id, new(bound.Value!.Revision, null), _ => true, default)).IsSuccess);
        var detached = (await store.GetAsync()).MongoDbIntegrations.Single();
        Assert.Null(detached.PasswordBinding);
        Assert.Null(detached.Password);
    }

    [Fact]
    public async Task SavedValidationUsesRotatedSecretAndRejectsEndpointChangesThroughOrdinaryEdits()
    {
        var store = new InMemoryFuseStore();
        var node = new DataStore(Guid.NewGuid(), "cache", null, "Redis", Guid.NewGuid(), null, null, [], Now, Now);
        var conn = new RedisIntegration(Guid.NewGuid(), "cache", node.Id, new("rediss://cache.test:6380"), "reader", "previous", Now, Now);
        var vault = new SecretProvider(Guid.NewGuid(), "keys", new("https://sample.vault.azure.net/"),
            SecretProviderAuthMode.ManagedIdentity, null, SecretProviderCapabilities.Read, Now, Now);
        await store.UpdateAsync(s => s with { SecretProviders = [vault], RedisIntegrations = [conn], DataStores = [node] });
        var user = Mock.Of<ICurrentUser>();
        var audit = new FakeAuditService();
        var azure = new Mock<IAzureKeyVaultClient>();
        var value = "rotating-one";
        azure.Setup(v => v.ReadSecretAsync(vault, "connector", null)).ReturnsAsync(() =>
            value == "" ? Result<string>.Failure("denied") : Result<string>.Success(value));
        var resolver = new IntegrationPasswords(store, azure.Object, audit, user);
        Assert.True((await resolver.BindAsync("redis", conn.Id, new(IntegrationPasswords.View(conn).Revision,
            new(vault.Id, "connector", null)), _ => true, default)).IsSuccess);
        var inspected = new List<string?>();
        var inspector = new Mock<IRedisInspector>(MockBehavior.Strict);
        inspector.Setup(i => i.ValidateAsync(conn.Endpoint, "reader", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Uri, string, string?, CancellationToken>((_, _, pw, _) => inspected.Add(pw))
            .ReturnsAsync(new RedisConnectionTestResult(true, true, "connected", null));
        var service = new RedisIntegrationService(store, inspector.Object, audit, user, resolver);
        Assert.False((await service.UpdateAsync(new(conn.Id, "cache", node.Id, new("redis://other:6379"), "reader", null))).IsSuccess);
        Assert.True((await service.ValidateSavedAsync(conn.Id)).IsSuccess);
        value = "rotating-two";
        Assert.True((await service.ValidateSavedAsync(conn.Id)).IsSuccess);
        value = "";
        var failed = await service.ValidateSavedAsync(conn.Id);
        Assert.False(failed.IsSuccess);
        Assert.Equal(IntegrationPasswords.Unavailable, failed.Error);
        Assert.Equal(new[] { "rotating-one", "rotating-two" }, inspected);
        var saved = await service.GetByIdAsync(conn.Id);
        Assert.True(saved!.HasPassword);
        Assert.Equal(conn.Endpoint, saved.Endpoint);
    }

    [Theory]
    [InlineData(false, true, true, true, 403)]
    [InlineData(true, false, true, true, 403)]
    [InlineData(true, true, false, true, 403)]
    [InlineData(true, true, true, false, 403)]
    [InlineData(true, true, true, true, 200)]
    public async Task BindingRequiresAuthenticatedExplicitGrantsEvenInUnrestrictedPosture(bool authenticated,
        bool bind, bool reveal, bool update, int status)
    {
        var store = new InMemoryFuseStore();
        var keys = new List<string>();
        if (bind) keys.Add(SecretProviderPermissions.BindIntegrationPasswordKey);
        if (reveal) keys.Add(SecretProviderPermissions.RevealSecretKey);
        if (update) keys.Add("redisintegrations:update");
        var role = new FuseRole(Guid.NewGuid(), "operator", "", keys, Now, Now);
        var redis = new RedisIntegration(Guid.NewGuid(), "redis", Guid.NewGuid(), new("redis://cache.test"), "reader", null, Now, Now);
        await store.UpdateAsync(s => s with { RedisIntegrations = [redis], SecurityContext = s.SecurityContext with
            { Posture = SecurityPosture.Unrestricted, Roles = [role] } });
        var service = new IntegrationPasswords(store, Mock.Of<IAzureKeyVaultClient>(), new FakeAuditService(), Mock.Of<ICurrentUser>());
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthenticationMiddleware.RoleIdClaimType, role.Id.ToString())],
            authenticated ? "session" : null));
        var controller = new IntegrationCredentialsController(store, service) { ControllerContext = new ControllerContext
            { HttpContext = new DefaultHttpContext { User = principal } } };
        var response = await controller.Bind("redis", redis.Id, new(IntegrationPasswords.View(redis).Revision, null), default);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(response.Result).StatusCode);
    }
}
