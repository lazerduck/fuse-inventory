using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Security.Services;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class EntraSettingsServiceTests
{
    private static UpdateEntraSettings Command() => new(
        true, Guid.NewGuid(), Guid.NewGuid(), "https://inventory.example.test", 60, "test-secret");

    [Theory]
    [InlineData("http://inventory.example.test", 60)]
    [InlineData("https://inventory.example.test/path", 60)]
    [InlineData("https://inventory.example.test?query=1", 60)]
    [InlineData("https://inventory.example.test", 0)]
    [InlineData("https://inventory.example.test", 121)]
    public async Task InvalidSettingsLeaveTheStoreUnchanged(string origin, int minutes)
    {
        var store = new InMemoryFuseStore();
        var original = store.Current;
        var protector = new Mock<IEntraSecretProtector>(MockBehavior.Strict);
        var service = new EntraSettingsService(store, protector.Object);

        var result = await service.UpdateSettings(Command() with { PublicOrigin = origin, SessionMinutes = minutes });

        Assert.False(result.IsSuccess);
        Assert.Same(original, store.Current);
        protector.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SettingsProtectTheSecretAndOnlyInvalidateEntraSessions()
    {
        var store = new InMemoryFuseStore();
        var protector = new Mock<IEntraSecretProtector>();
        protector.Setup(value => value.Protect("test-secret")).Returns("protected-secret");
        var service = new EntraSettingsService(store, protector.Object);
        var userId = Guid.NewGuid();
        var local = new Session("local-session", userId, DateTime.UtcNow.AddHours(1));
        var entra = new Session("entra-session", userId, DateTime.UtcNow.AddHours(1)) { IsEntra = true };
        await store.UpdateAsync(snapshot => snapshot with
        {
            SecurityContext = snapshot.SecurityContext with { Sessions = [local, entra] }
        });

        Assert.True((await service.UpdateSettings(Command())).IsSuccess);

        var security = store.Current!.SecurityContext;
        Assert.Equal("protected-secret", security.Entra.ProtectedClientSecret);
        Assert.Equal(SecurityPosture.FullyRestricted, security.Posture);
        Assert.Equal(local, Assert.Single(security.Sessions));
        Assert.True((await service.GetSettings()).HasClientSecret);
    }

    [Fact]
    public async Task SameConnectionRetainsSecretButChangingClientRequiresANewSecret()
    {
        var store = new InMemoryFuseStore();
        var protector = new Mock<IEntraSecretProtector>();
        protector.Setup(value => value.Protect(It.IsAny<string>())).Returns("protected-secret");
        var service = new EntraSettingsService(store, protector.Object);
        var command = Command();
        Assert.True((await service.UpdateSettings(command)).IsSuccess);
        var initialVersion = store.Current!.SecurityContext.Entra.Version;

        Assert.True((await service.UpdateSettings(command with { ClientSecret = null })).IsSuccess);
        Assert.Equal("protected-secret", store.Current.SecurityContext.Entra.ProtectedClientSecret);
        Assert.NotEqual(initialVersion, store.Current.SecurityContext.Entra.Version);

        var original = store.Current;
        var changedClient = command with { ClientId = Guid.NewGuid(), ClientSecret = null };
        Assert.False((await service.UpdateSettings(changedClient)).IsSuccess);
        Assert.Same(original, store.Current);
        protector.Verify(value => value.Protect(It.IsAny<string>()), Times.Once);
    }
}
