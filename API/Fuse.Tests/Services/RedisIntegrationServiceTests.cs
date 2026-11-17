using System.Text.Json;
using Fuse.Core.Areas.RedisIntegration;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class RedisIntegrationServiceTests
{
    private static (InMemoryFuseStore Store, DataStore DataStore) Fixture(string kind = "Redis")
    {
        var dataStore = new DataStore(Guid.NewGuid(), "Redis", null, kind, Guid.NewGuid(), null,
            new Uri("redis://localhost:6379"), [], DateTime.UtcNow, DateTime.UtcNow);
        return (new InMemoryFuseStore(new InMemoryFuseStore().Current! with { DataStores = [dataStore] }), dataStore);
    }

    private static RedisIntegrationService Service(InMemoryFuseStore store, IRedisInspector? inspector = null, FakeAuditService? audit = null) =>
        new(store, inspector ?? new Mock<IRedisInspector>(MockBehavior.Strict).Object, audit ?? new FakeAuditService(), new FakeCurrentUser());

    [Fact]
    public async Task ConfigurationCrudDoesNotConnectAndPasswordsNeverAppearInResponsesOrAudit()
    {
        var (store, dataStore) = Fixture();
        var audit = new FakeAuditService();
        var service = Service(store, audit: audit);
        var created = await service.CreateAsync(new(" Redis lab ", dataStore.Id, dataStore.ConnectionUri!, "inspector", "private-inspection-password"));
        Assert.True(created.IsSuccess);
        Assert.Equal("Redis lab", created.Value!.Name);
        Assert.True(created.Value.HasPassword);
        Assert.Equal("private-inspection-password", Assert.Single(store.Current!.RedisIntegrations).Password);
        Assert.DoesNotContain("private-inspection-password", JsonSerializer.Serialize(created));
        Assert.DoesNotContain("private-inspection-password", JsonSerializer.Serialize(await service.GetAllAsync()));
        Assert.DoesNotContain("private-inspection-password", JsonSerializer.Serialize(await service.GetByIdAsync(created.Value.Id)));

        var retained = await service.UpdateAsync(new(created.Value.Id, "Renamed", dataStore.Id, dataStore.ConnectionUri!, "inspector", null));
        Assert.True(retained.IsSuccess);
        Assert.True(retained.Value!.HasPassword);
        Assert.Equal("private-inspection-password", Assert.Single(store.Current.RedisIntegrations).Password);

        var cleared = await service.UpdateAsync(new(created.Value.Id, "Renamed", dataStore.Id, dataStore.ConnectionUri!, "inspector", ""));
        Assert.True(cleared.IsSuccess);
        Assert.False(cleared.Value!.HasPassword);
        Assert.Equal("", Assert.Single(store.Current.RedisIntegrations).Password);
        Assert.True((await service.DeleteAsync(new(created.Value.Id))).IsSuccess);
        Assert.Empty(store.Current.RedisIntegrations);
        Assert.Equal(4, audit.Logs.Count);
        Assert.All(audit.Logs, log => Assert.Equal(AuditArea.RedisIntegration, log.Area));
        Assert.DoesNotContain("private-inspection-password", JsonSerializer.Serialize(audit.Logs));
        Assert.Contains("passwordChanged", audit.Logs[0].ChangeDetails);
    }

    [Fact]
    public async Task CredentialsAreUsedOnlyByExplicitValidationAndDefaultUsernameIsConsistent()
    {
        var (store, dataStore) = Fixture();
        var inspector = new Mock<IRedisInspector>(MockBehavior.Strict);
        inspector.Setup(x => x.ValidateAsync(dataStore.ConnectionUri!, "default", "private-password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RedisConnectionTestResult(true, true, "connected", null));
        var service = Service(store, inspector.Object);
        var created = await service.CreateAsync(new("Redis", dataStore.Id, dataStore.ConnectionUri!, null, "private-password"));
        Assert.True(created.IsSuccess);
        inspector.VerifyNoOtherCalls();
        Assert.Equal("default", created.Value!.Username);
        var result = await service.ValidateSavedAsync(created.Value.Id);
        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsSuccessful);
        inspector.Verify(x => x.ValidateAsync(dataStore.ConnectionUri!, "default", "private-password", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("http://localhost:6379")]
    [InlineData("redis://user:private-password@localhost:6379")]
    [InlineData("redis://localhost:6379/1")]
    [InlineData("redis://localhost:6379/?password=private-password")]
    [InlineData("redis://localhost:6379/#private-password")]
    [InlineData("redis://localhost:0")]
    public async Task InvalidEndpointsAreRejectedWithoutEchoingSecrets(string value)
    {
        var (store, dataStore) = Fixture();
        var service = Service(store);
        var created = await service.CreateAsync(new("Redis", dataStore.Id, new Uri(value), null, null));
        Assert.False(created.IsSuccess);
        Assert.Equal(ErrorType.Validation, created.ErrorType);
        Assert.DoesNotContain("private-password", created.Error);
        var validation = await service.ValidateAsync(new(new Uri(value), null, null));
        Assert.False(validation.IsSuccess);
        Assert.Empty(store.Current!.RedisIntegrations);
    }

    [Fact]
    public async Task DatastoreMustExistAndBeRedisAndOnlyHaveOneIntegration()
    {
        var (store, dataStore) = Fixture();
        var service = Service(store);
        var absent = await service.CreateAsync(new("Redis", Guid.NewGuid(), dataStore.ConnectionUri!, null, null));
        Assert.Equal(ErrorType.NotFound, absent.ErrorType);
        var (sqlStore, sqlDataStore) = Fixture("sql");
        var wrongKind = await Service(sqlStore).CreateAsync(new("Redis", sqlDataStore.Id, dataStore.ConnectionUri!, null, null));
        Assert.Equal(ErrorType.Validation, wrongKind.ErrorType);
        Assert.True((await service.CreateAsync(new("Redis", dataStore.Id, dataStore.ConnectionUri!, null, null))).IsSuccess);
        Assert.Equal(ErrorType.Conflict, (await service.CreateAsync(new("Duplicate", dataStore.Id, dataStore.ConnectionUri!, null, null))).ErrorType);
        Assert.Single(store.Current!.RedisIntegrations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingNameIsRejectedEvenWhenJsonProvidesNull(string? name)
    {
        var (store, dataStore) = Fixture();
        var result = await Service(store).CreateAsync(new(name!, dataStore.Id, dataStore.ConnectionUri!, null, null));
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("user\nname")]
    public void AmbiguousOrControlCharacterUsernamesAreRejected(string username) =>
        Assert.False(RedisConnectionSettings.Validate(new Uri("redis://localhost"), username, null).IsSuccess);

    [Fact]
    public async Task DiscoveryScopesAccountsToDataStoreWithoutChangingDesiredGrants()
    {
        var (store, dataStore) = Fixture();
        var wanted = new Account(Guid.NewGuid(), dataStore.Id, TargetKind.DataStore, AuthKind.UserPassword,
            new(SecretBindingKind.None, null, null), "reader", null,
            [new(Guid.NewGuid(), "SqlGrantNotRedisAcl", null, [Privilege.Select])], [], DateTime.UtcNow, DateTime.UtcNow);
        await store.UpdateAsync(s => s with { Accounts = [wanted, wanted with { Id = Guid.NewGuid(), TargetId = Guid.NewGuid() }] });
        var inspector = new Mock<IRedisInspector>(MockBehavior.Strict);
        inspector.Setup(x => x.DiscoverAsync(It.IsAny<RedisIntegration>(), It.Is<IReadOnlyList<Account>>(a => a.Count == 1 && a[0] == wanted), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RedisDiscoveryResponse(Guid.Empty, DateTime.UtcNow, "connected", "denied", "denied", false, null, [], [], []));
        var service = Service(store, inspector.Object);
        var created = await service.CreateAsync(new("Redis", dataStore.Id, dataStore.ConnectionUri!, "inspector", "password"));
        Assert.True((await service.DiscoverAsync(created.Value!.Id)).IsSuccess);
        Assert.Equal(wanted, store.Current!.Accounts.Single(x => x.Id == wanted.Id));
        inspector.VerifyAll();
    }
}
