using System.Text.Json;
using Fuse.API.Controllers;
using Fuse.API.CurrentUser;
using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public sealed class AzureSecretReferenceTests
{
    private static SecretProvider Provider(string host) => new(Guid.NewGuid(), "Lab", new Uri("https://" + host), SecretProviderAuthMode.ManagedIdentity,
        null, SecretProviderCapabilities.Check, default, default);

    [Theory]
    [InlineData("https://lab.vault.azure.net/secrets/orders/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 200)]
    [InlineData("https://different.vault.azure.net/secrets/orders/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 404)]
    [InlineData("https://lab.vault.azure.net/secrets/orders/malformed/version", 400)]
    [InlineData("https://lab.vault.azure.net:1234/secrets/orders", 400)]
    public async Task ConfigurationReferenceUsesOnlyVersionMetadataFromConfiguredVault(string reference, int status)
    {
        var vault = Provider("lab.vault.azure.net");
        var config = Provider("lab.azconfig.io");
        var store = new InMemoryFuseStore();
        var instance = new ApplicationInstance(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null, [], [], default, default,
            null, config.Id, "orders");
        var app = new Application(Guid.NewGuid(), "Ordering", null, null, null, null, null, null, null, [], [instance], [], default, default);
        await store.UpdateAsync(s => s with { SecretProviders = [config, vault], Applications = [app] });
        var secret = new Mock<IAzureKeyVaultClient>(MockBehavior.Strict);
        var version = new KeyVaultVersion("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", null, DateTimeOffset.UtcNow.AddDays(1), true);
        secret.Setup(s => s.VersionsAsync(vault, "orders", default)).ReturnsAsync(new KeyVaultVersions("visible", [version]));
        var settings = new Mock<IAzureAppConfigurationClient>(MockBehavior.Strict);
        settings.Setup(s => s.GetKeyValueAsync(config, "connection:orders", "prod"))
            .ReturnsAsync(Result<AppConfigurationEntry?>.Success(new AppConfigurationEntry("connection:orders", "never-project-value", "prod",
                null, null, false, true, reference)));
        var controller = new AzureSecretReferencesController(store, secret.Object, settings.Object,
            new RequestPermissions([new SecretProviderPermissions(), new ApplicationPermissions(), new AccountPermissions()]))
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var response = await controller.Configuration(config.Id, "connection:orders", "prod", default);
        if (status == 400) Assert.IsType<BadRequestResult>(response.Result);
        else if (status == 404) Assert.IsType<NotFoundResult>(response.Result);
        else
        {
            var report = Assert.IsType<SecretReferenceHealth>(Assert.IsType<OkObjectResult>(response.Result).Value);
            Assert.Equal(version.Version, report.PinnedVersion);
            Assert.Equal(app.Id, Assert.Single(report.Consumers).ApplicationId);
            Assert.DoesNotContain("never-project-value", JsonSerializer.Serialize(report));
        }
        secret.Verify(s => s.VersionsAsync(vault, "orders", default), status == 200 ? Times.Once() : Times.Never());
        secret.VerifyNoOtherCalls();
    }
}
