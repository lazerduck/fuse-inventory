using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fuse.API.Controllers;
using Fuse.API.Updates;
using Xunit;

namespace Fuse.Tests.Services;

public class PublishedImageTests
{
    private const string OldDate = "2026-10-01T00:00:00Z";
    private const string NewDate = "2026-10-02T00:00:00Z";

    [Theory]
    [InlineData("release", "latest", "0.9.0", "0.10.0", "aaaa", NewDate, "0.10.0")]
    [InlineData("release", "latest", "0.10.0", "0.9.0", "aaaa", NewDate, null)]
    [InlineData("release", "latest", "0.9.0", "0.9.0", "aaaa", NewDate, null)]
    [InlineData("pre-release", "pre-release", "0.9.0-pre.ffff", "0.9.0-pre.aaaa", "aaaa", NewDate, "0.9.0-pre.aaaa")]
    [InlineData("pre-release", "pre-release", "0.9.0-pre.ffff", "0.9.0-pre.ffff", "ffff", NewDate, null)]
    [InlineData("pre-release", "pre-release", "0.9.0-pre.ffff", "0.9.0-pre.aaaa", "aaaa", OldDate, null)]
    public async Task ChecksThePublishedChannelImage(string channel, string tag, string currentVersion,
        string publishedVersion, string commit, string date, string? expected)
    {
        var build = new AboutResponse("Fuse.Inventory", currentVersion, channel, "ffff", "ffff", OldDate, UpdateCheckStatus.Checking);
        using var client = new HttpClient(new Registry(tag, channel, publishedVersion, commit, date));
        Assert.Equal(expected, await PublishedImage.GetAvailableVersionAsync(client, build, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsAnImageFromTheWrongChannel()
    {
        var build = new AboutResponse("Fuse.Inventory", "0.1.0", "release", "ffff", "ffff", OldDate, UpdateCheckStatus.Checking);
        using var client = new HttpClient(new Registry("latest", "pre-release", "0.2.0", "aaaa", NewDate));
        await Assert.ThrowsAsync<InvalidDataException>(() => PublishedImage.GetAvailableVersionAsync(client, build, CancellationToken.None));
    }

    private sealed class Registry(string tag, string channel, string version, string commit, string date) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var root = "/v2/lazerduck/fuse-inventory";
            var manifest = "sha256:" + new string('a', 64);
            var config = "sha256:" + new string('b', 64);
            var path = request.RequestUri!.AbsolutePath;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("ghcr.io", request.RequestUri.Host);
            if (path != "/token") Assert.Equal("Bearer read-only", request.Headers.Authorization?.ToString());
            object body;
            if (path == "/token")
                body = new { token = "read-only" };
            else if (path == $"{root}/manifests/{tag}")
                body = new { manifests = new[] {
                    new { platform = new { os = "unknown", architecture = "unknown" }, digest = "attestation" },
                    new { platform = new { os = "linux", architecture = "amd64" }, digest = manifest }
                } };
            else if (path == $"{root}/manifests/{manifest}")
                body = new { config = new { digest = config } };
            else if (path == $"{root}/blobs/{config}")
                body = new { config = new { Env = new[] { $"APP_CHANNEL={channel}", $"APP_VERSION={version}",
                    $"GIT_COMMIT_ID={commit}", $"BUILD_DATE={date}" } } };
            else
                throw new InvalidOperationException($"Unexpected registry request {path}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: JsonSerializerOptions.Default) });
        }
    }
}
