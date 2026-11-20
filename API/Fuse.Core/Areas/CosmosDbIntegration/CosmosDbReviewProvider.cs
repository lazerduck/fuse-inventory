using System.Globalization;
using System.Text.Json;
using Fuse.Core.Areas.AzureMetadata;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Models;
using static Fuse.Core.Areas.AzureMetadata.AzureReview;

namespace Fuse.Core.Areas.CosmosDbIntegration;

public sealed class CosmosDbReviewProvider(CosmosDbInspector inspector) : IIntegrationReviewProvider
{
    public string Provider => "cosmosdb";
    public IReadOnlyList<ReviewCheck> Checks { get; } =
    [
        new("container-policy", "Container configuration", [
            new("database", "Database"), new("container", "Container"),
            new("partitionPaths", "Ordered partition paths", false, Hint: "JSON array, for example [\"/tenantId\", \"/category\"]"),
            new("ttl", "Default TTL", false, Hint: "disabled, -1 for no default expiry, or a positive number of seconds"),
            new("indexingPolicy", "Expected indexing sections", false, Hint: "A JSON object with the policy sections to compare")]),
        new("database-throughput", "Shared database throughput", [new("database", "Database"),
            new("mode", "Mode", Choices: ["manual", "autoscale"]), new("ru", "Provisioned or maximum RU/s")]),
        new("data-role", "Cosmos data-role assignment", [new("database", "Database"), new("container", "Container"),
            new("identityId", "Fuse identity"), new("action", "Data action", Choices: ["items/read", "executeQuery", "items/create", "items/replace", "items/delete"])])
    ];

    public string? Validate(Snapshot snapshot, ReviewInput input)
    {
        var item = snapshot.CosmosDbIntegrations.FirstOrDefault(c => c.Id == input.IntegrationId);
        if (item is null || !CosmosDbInspector.Valid(item.AccountResourceId, item.AuthMode)) return "Choose a Cosmos DB integration.";
        if (input.AccountId is not null) return "Cosmos checks use a container or a bound Fuse identity.";
        string? db = Field(input, "database"), container = Field(input, "container");
        if (db is null || !CosmosDbInspector.ValidTarget(db, container)) return "Specify a valid database and container name.";
        if (input.Kind == "database-throughput")
            return int.TryParse(Field(input, "ru"), NumberStyles.None, CultureInfo.InvariantCulture, out var ru) && ru > 0 &&
                Field(input, "mode") is "manual" or "autoscale" ? null : "Select the throughput mode and a positive RU/s amount.";
        if (container is null) return "Specify a container name.";
        if (input.Kind == "data-role")
            return AzureRoleReview.Identity(snapshot, input) is null ? "Select a Fuse identity with its Azure tenant and principal object IDs." : null;
        if (input.Kind != "container-policy") return "Unknown Cosmos check.";
        string? ttl = Field(input, "ttl"), paths = Field(input, "partitionPaths"), policy = Field(input, "indexingPolicy");
        if (ttl is null && paths is null && policy is null) return "Choose at least one container expectation.";
        if (ttl is not null && ttl != "disabled" && !(int.TryParse(ttl, out var seconds) && (seconds == -1 || seconds > 0)))
            return "TTL must be disabled, -1, or a positive number of seconds.";
        try
        {
            if (paths is not null)
            {
                using var json = JsonDocument.Parse(paths);
                if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() is < 1 or > 3 ||
                    json.RootElement.EnumerateArray().Any(p => p.ValueKind != JsonValueKind.String || p.GetString()?.StartsWith('/') != true))
                    return "Enter a JSON array of one to three partition paths.";
            }
            if (policy is not null)
            {
                using var json = JsonDocument.Parse(policy);
                if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.EnumerateObject().Any())
                    return "Enter an indexing policy JSON object with at least one section.";
            }
        }
        catch (JsonException) { return "Enter valid JSON for partition paths and indexing policy."; }
        return null;
    }

    public async Task<ReviewEvidence> InspectAsync(Snapshot snapshot, ReviewInput input, CancellationToken ct)
    {
        var item = snapshot.CosmosDbIntegrations.Single(c => c.Id == input.IntegrationId);
        var database = input.Fields["database"];
        var container = Field(input, "container");
        var result = new AzureReview();
        if (input.Kind == "data-role")
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            var session = await inspector.Open(item.AccountResourceId, item.AuthMode, item.CredentialProfileId, ct, deadline.Token);
            return await AzureRoleReview.Cosmos(session, item.AccountResourceId, database, container!, AzureRoleReview.Identity(snapshot, input)!, input.Fields["action"]);
        }
        if (input.Kind == "database-throughput")
        {
            var throughput = await inspector.SharedThroughputAsync(item, database, ct);
            if (throughput.Visibility != "visible" || throughput.AutoscaleMaxRu is null && throughput.ManualRu is null)
                result.Unknown("Shared database throughput: " + throughput.Visibility);
            else
            {
                result.Compare("Throughput mode", input.Fields["mode"], throughput.AutoscaleMaxRu is null ? "manual" : "autoscale");
                result.Compare("Database RU/s", int.Parse(input.Fields["ru"], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                    (throughput.AutoscaleMaxRu ?? throughput.ManualRu)!.Value.ToString(CultureInfo.InvariantCulture));
            }
            return result.Result(database);
        }
        var observation = await inspector.DiscoverAsync(item, database, container, ct);
        var row = observation.Databases.SelectMany(db => db.Containers).FirstOrDefault(c => c.Name == container);
        if (observation.Visibility != "direct" || row is null)
            result.Unknown("Container metadata: " + observation.Visibility);
        else
        {
            if (Field(input, "partitionPaths") is { } paths)
            {
                using var expected = JsonDocument.Parse(paths);
                result.Compare("Ordered partition paths", Canonical(expected.RootElement),
                    row.PartitionKey is null ? null : JsonSerializer.Serialize(row.PartitionKey.Paths));
            }
            if (Field(input, "ttl") is { } ttl)
                result.Compare("Default TTL", ttl == "disabled" ? ttl : int.Parse(ttl, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                    row.DefaultTtl?.ToString(CultureInfo.InvariantCulture) ?? "disabled");
            if (Field(input, "indexingPolicy") is { } policy)
            {
                if (row.IndexingPolicy is null) result.Unknown("Indexing policy unavailable.");
                else
                {
                    using var expected = JsonDocument.Parse(policy);
                    using var actual = JsonDocument.Parse(row.IndexingPolicy.PolicyJson);
                    foreach (var section in expected.RootElement.EnumerateObject())
                        result.Compare("Indexing " + section.Name, Canonical(section.Value, section.Name),
                            actual.RootElement.TryGetProperty(section.Name, out var value) ? Canonical(value, section.Name) : "(absent)");
                }
            }
        }
        return result.Result(database + "/" + container);
    }
}
