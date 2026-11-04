using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Fuse.API.Controllers;

namespace Fuse.API.Updates;

public static class PublishedImage
{
    private const string Repository = "lazerduck/fuse-inventory";
    private const string Registry = "https://ghcr.io/v2/" + Repository;
    private const string ManifestTypes = "application/vnd.oci.image.index.v1+json, application/vnd.oci.image.manifest.v1+json, " +
        "application/vnd.docker.distribution.manifest.list.v2+json, application/vnd.docker.distribution.manifest.v2+json";

    public static async Task<string?> GetAvailableVersionAsync(HttpClient client, AboutResponse running, CancellationToken ct)
    {
        var tag = running.Channel switch
        {
            "release" => "latest",
            "pre-release" => "pre-release",
            _ => throw new ArgumentException("Unsupported image channel.")
        };

        var auth = await client.GetFromJsonAsync<JsonElement>(
            $"https://ghcr.io/token?service=ghcr.io&scope=repository:{Repository}:pull", ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("token").GetString());
        client.DefaultRequestHeaders.Accept.ParseAdd(ManifestTypes);
        var manifest = await client.GetFromJsonAsync<JsonElement>($"{Registry}/manifests/{tag}", ct);
        if (manifest.TryGetProperty("manifests", out var manifests))
        {
            // CI stamps the same build metadata on both architectures. Skip provenance manifests.
            var image = manifests.EnumerateArray().First(item =>
                item.TryGetProperty("platform", out var platform) &&
                platform.GetProperty("os").GetString() == "linux" && platform.GetProperty("architecture").GetString() == "amd64");
            manifest = await client.GetFromJsonAsync<JsonElement>($"{Registry}/manifests/{Digest(image)}", ct);
        }

        var config = await client.GetFromJsonAsync<JsonElement>($"{Registry}/blobs/{Digest(manifest.GetProperty("config"))}", ct);
        var env = config.GetProperty("config").GetProperty("Env").EnumerateArray()
            .Select(item => item.GetString()!.Split('=', 2)).Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

        if (env["APP_CHANNEL"] != running.Channel || string.IsNullOrWhiteSpace(env["APP_VERSION"]))
            throw new InvalidDataException("The published image has inconsistent build metadata.");

        var version = env["APP_VERSION"];
        if (running.Channel == "release")
            return Version.Parse(version) > Version.Parse(running.Version) ? version : null;

        var commit = env["GIT_COMMIT_ID"];
        if (string.IsNullOrWhiteSpace(commit) || running.GitCommitId == "unknown")
            throw new InvalidDataException("Missing pre-release commit.");
        if (commit == running.GitCommitId)
            return null;

        return DateTimeOffset.Parse(env["BUILD_DATE"], CultureInfo.InvariantCulture) >
            DateTimeOffset.Parse(running.BuildDate, CultureInfo.InvariantCulture) ? version : null;
    }

    private static string Digest(JsonElement descriptor)
    {
        var digest = descriptor.GetProperty("digest").GetString();
        if (digest is null || !digest.StartsWith("sha256:", StringComparison.Ordinal) ||
            digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid image digest.");
        return digest;
    }
}
