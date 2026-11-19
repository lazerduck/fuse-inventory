using System.Security.Claims;
using Fuse.API.Controllers;
using Fuse.API.CurrentUser;
using Fuse.API.Middleware;
using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.DataStore;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Areas.KeycloakIntegration;
using Fuse.Core.Areas.MessageBroker;
using Fuse.Core.Areas.MongoDbIntegration;
using Fuse.Core.Areas.PostgreSqlIntegration;
using Fuse.Core.Areas.RabbitMqIntegration;
using Fuse.Core.Areas.RedisIntegration;
using Fuse.Core.Areas.ServiceBusIntegration;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Tests.TestInfrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class ReviewImportTests
{
    private readonly InMemoryFuseStore store = new();
    private readonly Mock<IRabbitMqIntegrationService> rabbit = new();
    private readonly Mock<IMongoDbIntegrationService> mongo = new();
    private readonly Mock<IKeycloakIntegrationService> kc = new();
    private readonly FakeAuditService audit = new();
    private readonly ReviewImportService service;
    private readonly DateTime now = DateTime.UtcNow;
    private readonly Guid brokerId = Guid.NewGuid(), integration = Guid.NewGuid(), instance = Guid.NewGuid(), env = Guid.NewGuid();
    private readonly string[] read = [IntegrationReviewPermissions.ImportKey, IntegrationReviewPermissions.ReadKey,
        RabbitMqIntegrationPermissions.ReadKey, MessageBrokerPermissions.ReadKey, ApplicationPermissions.ReadKey];
    private readonly Guid roleId = Guid.NewGuid();
    private string vhost = "east", coverage = "complete";
    private bool hasQueue = true;

    public ReviewImportTests()
    {
        service = new(store, audit, Mock.Of<ICurrentUser>(), Mock.Of<IPostgreSqlIntegrationService>(), Mock.Of<IRedisIntegrationService>(),
            mongo.Object, rabbit.Object, kc.Object, Mock.Of<IServiceBusIntegrationService>());
        rabbit.Setup(r => r.DiscoverAsync(integration, It.IsAny<CancellationToken>())).ReturnsAsync(() =>
            Result<RabbitMqDiscoveryResponse>.Success(new(integration, now, vhost, "connected", coverage, "complete", "denied",
                hasQueue ? [new("orders", "queues", vhost, "classic")] : [], [], ["publisher"], [], [], [])));
    }
    private async Task Seed()
    {
        var broker = new MessageBroker(brokerId, "primary", null, "RabbitMQ", env, null, [], now, now);
        var conn = new RabbitMqIntegration(integration, "broker", brokerId, vhost, new("https://broker.test"), "inspector", null, now, now);
        var app = new Application(Guid.NewGuid(), "worker", null, null, null, null, null, null, null, [],
            [new(instance, env, null, null, null, null, null, [], [], now, now)], [], now, now);
        await store.UpdateAsync(s => s with { Applications = [app], RabbitMqIntegrations = [conn], MessageBrokers = [broker] });
    }
    private async Task Grant(params string[] writes) => await store.UpdateAsync(s => s with {
        SecurityContext = s.SecurityContext with { Posture = SecurityPosture.FullyRestricted,
            Roles = [new(roleId, "tester", "", [..read, ..writes], now, now)] }
    });
    private ReviewImportsController Controller()
    {
        var controller = new ReviewImportsController(service, store, new RequestPermissions([
            new IntegrationReviewPermissions(), new RabbitMqIntegrationPermissions(), new MessageBrokerPermissions(),
            new AccountPermissions(), new ApplicationPermissions(), new DataStorePermissions()]), new EphemeralDataProtectionProvider());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(AuthenticationMiddleware.RoleIdClaimType,
                roleId.ToString())], "session")) } };
        return controller;
    }
    private ImportSelection Queue() => new("rabbitmq", integration, new("queue", "orders", vhost), instance);

    [Fact]
    public async Task SignedQueueImportRechecksExactPermissionsAndPresenceAndCanBeReplayed()
    {
        await Seed();
        var controller = Controller();
        await Grant(MessageBrokerPermissions.UpdateKey, ApplicationPermissions.UpdateKey);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>((await controller.Preview(Queue(), default)).Result).StatusCode);
        await Grant(MessageBrokerPermissions.UpdateKey, ApplicationPermissions.UpdateInstanceKey);
        coverage = "partial";
        var preview = Assert.IsType<ImportPreview>(Assert.IsType<OkObjectResult>((await controller.Preview(Queue(), default)).Result).Value);
        Assert.Equal("limited", preview.Selection.Item.Coverage);
        Assert.IsType<ConflictObjectResult>((await controller.Confirm(preview with { Destination = "wrong" }, default)).Result);
        await Grant(ApplicationPermissions.UpdateInstanceKey);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>((await controller.Confirm(preview, default)).Result).StatusCode);
        await Grant(MessageBrokerPermissions.UpdateKey, ApplicationPermissions.UpdateInstanceKey);
        hasQueue = false;
        Assert.Equal(409, Assert.IsType<ObjectResult>((await controller.Confirm(preview, default)).Result).StatusCode);
        hasQueue = true;
        var jsonDate = DateTimeOffset.FromUnixTimeMilliseconds(new DateTimeOffset(preview.ConnectionVersion).ToUnixTimeMilliseconds()).UtcDateTime;
        var confirmed = (await controller.Confirm(preview with { ConnectionVersion = jsonDate }, default)).Result;
        var receipt = Assert.IsType<ImportReceipt>(Assert.IsType<OkObjectResult>(confirmed).Value);
        var again = Assert.IsType<ImportReceipt>(Assert.IsType<OkObjectResult>((await controller.Confirm(preview, default)).Result).Value);
        Assert.Equal(new ImportReceipt(receipt.InventoryId, false, false), again);
        var saved = await store.GetAsync();
        Assert.Equal("limited", Assert.Single(saved.MessageBrokers.Single().Queues!).ImportedFrom?.Coverage);
        Assert.Single(saved.Applications.Single().Instances.Single().Dependencies);
        Assert.Single(audit.Logs);
        vhost = "west";
        await store.UpdateAsync(s => s with { RabbitMqIntegrations = [s.RabbitMqIntegrations.Single() with { VirtualHost = vhost }] });
        Assert.Equal(ErrorType.Conflict, (await service.PreviewAsync(Queue(), default)).ErrorType);
    }

    [Fact]
    public async Task DenialDuringCommitAndInterveningManualRecordRequireFreshPreview()
    {
        await Seed();
        var preview = (await service.PreviewAsync(Queue(), default)).Value!;
        Assert.Equal(ErrorType.Unauthorized, (await service.ApplyAsync(preview, default, (_, _) => false)).ErrorType);
        await store.UpdateAsync(s => s with { MessageBrokers = [s.MessageBrokers.Single() with {
            Queues = [new(Guid.NewGuid(), "orders", "documented manually")] }] });
        Assert.Equal(ErrorType.Conflict, (await service.ApplyAsync(preview, default)).ErrorType);
        var adoption = (await service.PreviewAsync(Queue(), default)).Value!;
        Assert.True(adoption.RecordOrigin);
        Assert.True((await service.ApplyAsync(adoption, default)).IsSuccess);
        var adopted = Assert.Single((await store.GetAsync()).MessageBrokers.Single().Queues!);
        Assert.Equal("documented manually", adopted.Description);
        Assert.Equal(vhost, adopted.ImportedFrom!.Scope);
    }

    [Fact]
    public async Task SameMongoLoginInTwoAuthenticationDatabasesCreatesIndependentInventory()
    {
        var target = new DataStore(Guid.NewGuid(), "mongo", null, "MongoDB", env, null, null, [], now, now);
        var conn = new MongoDbIntegration(integration, "mongo", target.Id, new("mongodb://mongo.test/db"), "inspect", null, now, now, "admin", false);
        await store.UpdateAsync(s => s with { DataStores = [target], MongoDbIntegrations = [conn] });
        string[] sources = ["app", "admin"];
        mongo.Setup(x => x.DiscoverAsync(integration, It.IsAny<CancellationToken>())).ReturnsAsync(Result<MongoDbDiscoveryResponse>.Success(
            new(integration, now, "db", "authenticated", "complete", [], "denied", [],
            sources.Select(x => new MongoDbUserScopeResponse(x, "complete")).ToArray(),
            sources.Select(x => new MongoDbUserResponse("same", x, [], [], [], "denied", [])).ToArray(), [], [])));
        foreach (var scope in sources)
        {
            var preview = (await service.PreviewAsync(new("mongodb", integration, new("account", "same", scope)), default)).Value!;
            Assert.True((await service.ApplyAsync(preview, default)).Value!.Created);
        }
        var accounts = (await store.GetAsync()).Accounts;
        Assert.Equal(sources.Order(), accounts.Select(a => a.ImportedFrom!.Scope).Order());
        Assert.All(accounts, a => Assert.Equal(a.Parameters!["authSource"], a.ImportedFrom!.Scope));
    }

    [Fact]
    public async Task SameKeycloakClientInTwoRealmsIsDistinctAndReplacementInvalidatesPreview()
    {
        await Seed();
        var nativeId = "alpha-client";
        var realms = new[] { "alpha", "beta" }.Select(name => new KeycloakIntegration(Guid.NewGuid(), name,
            new($"https://kc.test/realms/{name}"), "reader", "protected", now, now)).ToArray();
        await store.UpdateAsync(s => s with { KeycloakIntegrations = realms });
        foreach (var realm in realms)
            kc.Setup(k => k.DiscoverAsync(realm.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() =>
                Result<KeycloakDiscoveryResponse>.Success(new(realm.Issuer, now, "connected", "limited",
                    [new(realm == realms[0] ? nativeId : "beta-client", "inventory", true, true, null, "denied", [], [])], [])));
        foreach (var realm in realms)
        {
            var preview = (await service.PreviewAsync(new("keycloak", realm.Id, new("client", "inventory", realm.Issuer.AbsoluteUri), instance), default)).Value!;
            if (realm == realms[0])
            {
                nativeId = "replacement";
                Assert.Equal(ErrorType.Conflict, (await service.ApplyAsync(preview, default)).ErrorType);
                nativeId = "alpha-client";
            }
            Assert.True((await service.ApplyAsync(preview, default)).Value!.Created);
        }
        Assert.Equal(2, (await store.GetAsync()).Accounts.Select(a => a.ImportedFrom!.NativeId).Distinct().Count());
    }
}
