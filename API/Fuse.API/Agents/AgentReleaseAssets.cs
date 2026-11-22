using System.Security.Cryptography;

namespace Fuse.API.Agents;

public sealed record AgentAssetChecksums(string InstallerSha256, string Amd64Sha256, string Arm64Sha256);

public sealed class AgentReleaseAssets(IWebHostEnvironment environment, IConfiguration configuration)
{
    private readonly string _directory = Path.GetFullPath(
        configuration["Agent:AssetsDirectory"] ?? "agent-assets", environment.ContentRootPath);

    public const string Installer = "install.sh";
    public const string Manifest = "SHA256SUMS";
    public const string Signature = "SHA256SUMS.sig";
    public const string Amd64 = "fuse-agent-amd64.deb";
    public const string Arm64 = "fuse-agent-arm64.deb";

    internal string FilePath(string name) => Path.Combine(_directory, name);

    public string? GetAgentVersion()
    {
        var path = FilePath("VERSION");
        if (!File.Exists(path)) return null;
        var version = File.ReadAllText(path).Trim();
        return version.Length is > 0 and <= 128 && char.IsAsciiDigit(version[0])
            && version.All(c => char.IsAsciiLetterOrDigit(c) || ".+-~:".Contains(c)) ? version : null;
    }

    public AgentAssetChecksums? GetChecksums()
    {
        if (!new[] { Installer, Manifest, Signature, Amd64, Arm64 }.All(name => File.Exists(FilePath(name))))
            return null;

        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(FilePath(Manifest)))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || parts[0].Length != 64 || !parts[0].All(Uri.IsHexDigit)
                || !hashes.TryAdd(parts[1], parts[0]))
                return null;
        }

        if (!hashes.TryGetValue(Amd64, out var amd64) || !hashes.TryGetValue(Arm64, out var arm64))
            return null;

        using var stream = File.OpenRead(FilePath(Installer));
        return new(Convert.ToHexStringLower(SHA256.HashData(stream)), amd64, arm64);
    }
}

public static class AgentDownloadEndpoints
{
    public static void MapAgentDownloads(this WebApplication app)
    {
        Map("/agent/install.sh", AgentReleaseAssets.Installer, "text/plain");
        Map("/agent/packages/SHA256SUMS", AgentReleaseAssets.Manifest, "text/plain");
        Map("/agent/packages/SHA256SUMS.sig", AgentReleaseAssets.Signature, "application/octet-stream");
        Map("/agent/packages/fuse-agent-amd64.deb", AgentReleaseAssets.Amd64, "application/vnd.debian.binary-package");
        Map("/agent/packages/fuse-agent-arm64.deb", AgentReleaseAssets.Arm64, "application/vnd.debian.binary-package");

        void Map(string route, string filename, string mediaType)
        {
            app.MapGet(route, (AgentReleaseAssets assets, HttpResponse response) =>
            {
                response.Headers.CacheControl = "no-store";
                var path = assets.FilePath(filename);
                return File.Exists(path) ? Results.File(path, mediaType) : Results.NotFound();
            }).ExcludeFromDescription();
        }
    }
}
