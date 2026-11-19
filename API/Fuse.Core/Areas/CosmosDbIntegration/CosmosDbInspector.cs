using System.Text.Json;
using System.Text.RegularExpressions;
using Fuse.Core.Areas.AzureMetadata;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using static Fuse.Core.Areas.AzureMetadata.ArmMetadataSession;

namespace Fuse.Core.Areas.CosmosDbIntegration;

public sealed partial class CosmosDbInspector(HttpClient http, IAzureIntegrationManagerService manager, ITokenCredentialFactory? factory = null)
{
    private const string Version = "2024-11-15";
    private readonly ITokenCredentialFactory credentials = factory ?? new DefaultTokenCredentialFactory();

    [GeneratedRegex(@"\A/subscriptions/([a-f0-9-]{36})/resourceGroups/([\p{L}\p{N}_.()\-]{1,90})/providers/Microsoft\.DocumentDB/databaseAccounts/[a-z0-9][a-z0-9-]{1,42}[a-z0-9]/?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AccountPattern();

    internal static bool Valid(string? id, SecretProviderAuthMode mode) => id is { Length: < 512 } && Enum.IsDefined(mode)
        && AccountPattern().Match(id) is { Success: true } match && Guid.TryParseExact(match.Groups[1].Value, "D", out _)
        && !match.Groups[2].Value.EndsWith('.');
    private static bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 255 && name is not ("." or "..")
        && !name.Any(c => char.IsControl(c) || c is '/' or '\\' or '?' or '#');
    internal static bool ValidTarget(string? database, string? container) => (database is null || ValidName(database))
        && (container is null || (database is not null && ValidName(container)));

    internal async Task<ArmMetadataSession> Open(string id, SecretProviderAuthMode mode, Guid? profileId, CancellationToken ct, CancellationToken deadline) =>
        new(http, await GetToken(manager, credentials, mode, deadline, profileId, id), ct, deadline, Version, StringComparison.Ordinal);

    // Use the logical, case-sensitive ID from Cosmos. Construct every request under the configured account.
    private static JsonElement Resource(JsonElement entry) => entry.GetProperty("properties").GetProperty("resource");
    private static string Name(JsonElement row)
    {
        var name = Required(Resource(row), "id");
        return ValidName(name) ? name : throw new InspectionError("invalid-response");
    }

    private static string[] IndexPaths(JsonElement policy, string key) => policy.TryGetProperty(key, out var paths)
        && paths.ValueKind != JsonValueKind.Null ? paths.EnumerateArray().Select(p => Required(p, "path")).ToArray() : [];

    private static CosmosDbContainer Container(JsonElement row)
    {
        var resource = Resource(row);
        CosmosDbPartitionKey? partition = null;
        if (resource.TryGetProperty("partitionKey", out var pk) && pk.ValueKind == JsonValueKind.Object)
            partition = new(Text(pk, "kind"), pk.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number
                ? version.GetInt32() : null, pk.GetProperty("paths").EnumerateArray()
                .Select(path => path.GetString() ?? throw new InspectionError("invalid-response")).ToArray());
        CosmosDbIndexingPolicy? indexing = null;
        if (resource.TryGetProperty("indexingPolicy", out var policy) && policy.ValueKind == JsonValueKind.Object)
            indexing = new(Text(policy, "indexingMode"), policy.TryGetProperty("automatic", out var automatic) && automatic.ValueKind != JsonValueKind.Null
                ? automatic.GetBoolean() : null, IndexPaths(policy, "includedPaths"), IndexPaths(policy, "excludedPaths"), policy.GetRawText());
        int? ttl = resource.TryGetProperty("defaultTtl", out var ttlValue) && ttlValue.ValueKind == JsonValueKind.Number
            ? ttlValue.GetInt32() : null;
        return new(Name(row), partition, indexing, ttl);
    }

    private static async Task<CosmosDbThroughput> Throughput(ArmMetadataSession session, string databasePath)
    {
        var read = await session.Resource(databasePath + "/throughputSettings/default", row =>
        {
            var settings = Resource(row);
            int? manual = settings.TryGetProperty("throughput", out var ru) && ru.ValueKind == JsonValueKind.Number ? ru.GetInt32() : null;
            int? autoscale = settings.TryGetProperty("autoscaleSettings", out var scale) && scale.ValueKind == JsonValueKind.Object &&
                scale.TryGetProperty("maxThroughput", out var max) && max.ValueKind == JsonValueKind.Number ? max.GetInt32() : null;
            return new CosmosDbThroughput("visible", autoscale is null ? manual : null, autoscale);
        });
        return read.Value ?? new CosmosDbThroughput(read.Status);
    }

    internal async Task<CosmosDbThroughput> SharedThroughputAsync(CosmosDbConnection item, string database, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var session = await Open(item.AccountResourceId, item.AuthMode, item.CredentialProfileId, ct, deadline.Token);
            return await Throughput(session, item.AccountResourceId + "/sqlDatabases/" + Uri.EscapeDataString(database));
        }
        catch (Exception ex) when (Failure(ex, ct) is { } reason) { return new(reason); }
    }

    private static string Status<T>(PageList<T> list) => list.Status == "visible" && list.Items.Count == 0 ? "none-observed" : list.Status;

    public async Task<CosmosDbConnectionTestResult> ValidateAsync(string id, SecretProviderAuthMode mode, CancellationToken ct, Guid? profileId = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var session = await Open(id, mode, profileId, ct, deadline.Token);
            var page = await session.List(id + "/sqlDatabases", Name, firstPageOnly: true);
            return new(page.Status == "visible", Status(page));
        }
        catch (Exception e) when (Failure(e, ct) is { } status) { return new(false, status); }
    }

    public async Task<CosmosDbDiscoveryResponse> DiscoverAsync(CosmosDbConnection item, string? database, string? container, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var observed = DateTimeOffset.UtcNow;
        var databases = new List<CosmosDbDatabase>();
        var collection = item.AccountResourceId + "/sqlDatabases";
        string visibility;
        try
        {
            var session = await Open(item.AccountResourceId, item.AuthMode, item.CredentialProfileId, ct, deadline.Token);
            if (container is not null)
            {
                var path = collection + "/" + Uri.EscapeDataString(database!) + "/containers/" + Uri.EscapeDataString(container);
                using var json = await session.Read(new Uri(Arm + path + "?api-version=" + Version));
                var row = Container(json.RootElement);
                if (row.Name != container) throw new InspectionError("invalid-response");
                databases.Add(new(database!, "direct", [row]));
                visibility = "direct";
            }
            else
            {
                var names = database is null ? await session.List(collection, Name) : new PageList<string>([database], "direct");
                visibility = Status(names);
                foreach (var name in names.Items.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                {
                    ct.ThrowIfCancellationRequested();
                    var databasePath = collection + "/" + Uri.EscapeDataString(name);
                    var containers = await session.List(databasePath + "/containers", Container);
                    var status = Status(containers);
                    if (database is not null) visibility = status;
                    databases.Add(new(name, status, containers.Items.DistinctBy(c => c.Name, StringComparer.Ordinal).OrderBy(c => c.Name, StringComparer.Ordinal).ToArray(),
                        await Throughput(session, databasePath)));
                }
            }
        }
        catch (Exception e) when (Failure(e, ct) is { } status) { visibility = status; }
        return new(item.AccountResourceId, observed, visibility, databases);
    }
}
