using Fuse.Core.Areas.License;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace Fuse.Tests.Services;

public class LicenseServiceTests
{
    private static LicenseService Service(InMemoryFuseStore store) =>
        new(store, Mock.Of<IHttpClientFactory>());

    [Fact]
    public async Task Status_IsUnlicensedWhenNoKeyIsStored()
    {
        var status = await Service(new InMemoryFuseStore()).GetStatusAsync();

        Assert.False(status.IsValid);
        Assert.Equal("unlicensed", status.Status);
    }

    [Theory]
    [InlineData("not-a-license", "License keys must start")]
    [InlineData("fuse-license:not_base64!", "not valid Base64Url")]
    [InlineData("fuse-license:AQ", "format is not supported")]
    public async Task SetLicense_RejectsMalformedKeysButPersistsTheirStatus(string key, string message)
    {
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { AppSettings = s.AppSettings with { LocalLicenseValidationOnly = true } });

        var status = await Service(store).SetLicenseAsync($"  {key}  ");

        Assert.False(status.IsValid);
        Assert.Equal("invalid", status.Status);
        Assert.Contains(message, status.Message);
        Assert.Equal(key, store.Current!.License!.LicenseKey);
        Assert.Equal("invalid", store.Current.License.Status);
    }

    [Fact]
    public async Task SetLicense_RejectsUnsupportedVersionAndInvalidSignature()
    {
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with { AppSettings = s.AppSettings with { LocalLicenseValidationOnly = true } });
        var service = Service(store);
        var unsupported = new byte[93];
        var signedShape = new byte[93];
        signedShape[0] = 1;

        var unsupportedResult = await service.SetLicenseAsync(Encode(unsupported));
        var signatureResult = await service.SetLicenseAsync(Encode(signedShape));

        Assert.Contains("format is not supported", unsupportedResult.Message);
        Assert.Contains("signature is invalid", signatureResult.Message);

        static string Encode(byte[] bytes) => "fuse-license:" + Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    [Fact]
    public async Task GetStatusAndRefresh_ReevaluateInvalidStoredLicense()
    {
        var checkedAt = DateTime.UtcNow.AddDays(-1);
        var store = new InMemoryFuseStore();
        await store.UpdateAsync(s => s with
        {
            AppSettings = s.AppSettings with { LocalLicenseValidationOnly = false },
            License = new LicenseState("bad-key", "active", LastCheckedUtc: checkedAt, Message: "old")
        });
        var service = Service(store);

        var status = await service.GetStatusAsync();
        await service.RefreshOnlineAsync();

        Assert.Equal("invalid", status.Status);
        Assert.False(status.IsValid);
        Assert.Equal("invalid", store.Current!.License!.Status);
        Assert.True(store.Current.License.LastCheckedUtc > checkedAt);
    }

    [Fact]
    public async Task Refresh_DoesNothingWithoutLicenseOrWhenLocalOnly()
    {
        var store = new InMemoryFuseStore();
        var service = Service(store);
        await service.RefreshOnlineAsync();
        Assert.Null(store.Current!.License);

        var state = new LicenseState("bad-key", "unchanged");
        await store.UpdateAsync(s => s with
        {
            AppSettings = s.AppSettings with { LocalLicenseValidationOnly = true },
            License = state
        });
        await service.RefreshOnlineAsync();

        Assert.Equal(state, store.Current.License);
    }
}
