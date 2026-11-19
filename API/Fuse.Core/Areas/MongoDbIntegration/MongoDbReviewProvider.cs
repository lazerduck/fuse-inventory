using Fuse.Core.Areas.SecretProvider;
using System.Globalization;
using System.Text.Json;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Models;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;
using MongoDB.Bson;
using static Fuse.Core.Areas.IntegrationReviews.DataStoreReview;

namespace Fuse.Core.Areas.MongoDbIntegration;

public sealed class MongoDbReviewProvider(IMongoDbSessionFactory factory, IntegrationPasswords? passwords = null) : IIntegrationReviewProvider
{
    public string Provider => "mongodb";
    private static readonly string[] Actions = ["find", "insert", "update", "remove", "listIndexes", "createIndex", "dropIndex"];
    public IReadOnlyList<ReviewCheck> Checks { get; } =
    [
        new("collection-action", "MongoDB collection access", [
            new("collection", "Collection"), new("action", "Action", Choices: Actions),
            new("expected", "Expected result", Choices: ["allow", "deny"])], true),
        new("collection-index", "MongoDB index definition", [
            new("collection", "Collection"), new("index", "Index name"),
            new("keys", "Ordered key document", Hint: "For example {\"tenantId\":1,\"createdAt\":-1}"),
            new("unique", "Unique", Choices: ["yes", "no"]),
            new("ttl", "Time to live in seconds", Hint: "Enter none for an index without expiry.")]),
        new("index-agreement", "Compare a MongoDB index across environments", [
            new("collection", "Collection"), new("index", "Index name"),
            new("peerIntegrationId", "Second MongoDB integration"),
            new("peerCollection", "Collection in second database", Required: false, Hint: "Leave empty to use the same collection.")])
    ];

    internal static bool ValidCollection(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200 &&
        !value.Any(char.IsControl) && !value.StartsWith("system.", StringComparison.Ordinal) && !value.StartsWith('$');
    public string? Validate(Snapshot snapshot, ReviewInput input)
    {
        var item = snapshot.MongoDbIntegrations.FirstOrDefault(i => i.Id == input.IntegrationId);
        if (ValidateScope(snapshot, item?.DataStoreId, "MongoDB", input, Checks) is { } error) return error;
        if (!ValidCollection(input.Fields["collection"])) return "Choose a named application collection.";
        if (input.Kind == "index-agreement")
        {
            if (!Guid.TryParse(input.Fields["peerIntegrationId"], out var peerId)
                || !snapshot.MongoDbIntegrations.Any(i => i.Id == peerId && i.Endpoint != item!.Endpoint))
                return "Choose a registered MongoDB integration for another database or server.";
            if (input.Fields.GetValueOrDefault("peerCollection") is { Length: > 0 } other && !ValidCollection(other))
                return "Choose a named application collection in the second database.";
        }
        if (input.Kind == "collection-action" && !MongoDbConnectionSettings.ValidDatabase(MongoDbIdentity.AuthSource(
                ScopedAccount(snapshot, input.AccountId, item!.DataStoreId)!.Parameters)))
            return "Set the account’s authSource parameter to its authentication database.";
        if (input.Kind == "collection-index" &&
            (ParseKeys(input.Fields["keys"]) is null || (input.Fields["ttl"] != "none" &&
                !int.TryParse(input.Fields["ttl"], NumberStyles.None, CultureInfo.InvariantCulture, out _))))
            return "Enter ordered index keys and a nonnegative TTL in seconds or none.";
        return null;
    }

    internal static IReadOnlyList<MongoDbIndexField>? ParseKeys(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var fields = new List<MongoDbIndexField>();
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var key = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.GetRawText();
                if (string.IsNullOrWhiteSpace(property.Name) || property.Name.Any(char.IsControl) || fields.Any(f => f.Field == property.Name)
                    || key is not ("1" or "-1" or "hashed" or "text" or "2d" or "2dsphere" or "2dsphere_bucket" or "geoHaystack" or "columnstore"))
                    return null;
                fields.Add(new(property.Name, key));
            }
            return fields.Count is > 0 and <= 32 ? fields : null;
        }
        catch (JsonException) { return null; }
    }

    public async Task<ReviewEvidence> InspectAsync(Snapshot snapshot, ReviewInput input, CancellationToken ct)
    {
        if (Validate(snapshot, input) is { } error) return Unknown(error);
        var item = snapshot.MongoDbIntegrations.Single(x => x.Id == input.IntegrationId);
        var credential = await IntegrationPasswords.ForUse(item, passwords, ct);
        if (!credential.IsSuccess) return new(ReviewStatus.Unknown, IntegrationPasswords.Unavailable, []);
        item = credential.Value!;
        var database = MongoDbConnectionSettings.Database(item.Endpoint);
        var collection = input.Fields["collection"];
        if (input.Kind == "index-agreement")
        {
            var peer = snapshot.MongoDbIntegrations.Single(n => n.Id == Guid.Parse(input.Fields["peerIntegrationId"]));
            var credentials = await IntegrationPasswords.ForUse(peer, passwords, ct);
            if (!credentials.IsSuccess) return Unknown(IntegrationPasswords.Unavailable);
            peer = credentials.Value!;
            var otherCollection = input.Fields.GetValueOrDefault("peerCollection") is { Length: > 0 } other ? other : collection;
            var inspector = new MongoDbInspector(factory);
            var left = await inspector.IndexesAsync(item, collection, ct);
            var right = await inspector.IndexesAsync(peer, otherCollection, ct);
            var name = input.Fields["index"];
            var first = left.Indexes.FirstOrDefault(i => i.Name == name);
            var second = right.Indexes.FirstOrDefault(i => i.Name == name);
            var scopes = new[] { $"{item.Name} / {database}.{collection}: {left.Status}.",
                $"{peer.Name} / {MongoDbConnectionSettings.Database(peer.Endpoint)}.{otherCollection}: {right.Status}." };
            if (first is null && left.Status is not ("complete" or "not-found")
                || second is null && right.Status is not ("complete" or "not-found"))
                return Unknown("The named index could not be verified in both environments.", scopes);
            if (first is null || second is null) return Compare(false, $"Index {name} is missing from at least one collection.", scopes);
            var differences = new List<string>(scopes);
            if (!first.Keys.SequenceEqual(second.Keys)) differences.Add("Index keys or their order differ.");
            if (first.Unique != second.Unique) differences.Add("Uniqueness differs.");
            if (first.ExpireAfterSeconds != second.ExpireAfterSeconds) differences.Add("Index expiry differs.");
            return Compare(differences.Count == scopes.Length, differences.Count == scopes.Length ? "Both indexes agree." : "The index definitions differ.", differences.ToArray());
        }
        if (input.Kind == "collection-index")
        {
            var result = await new MongoDbInspector(factory).IndexesAsync(item, collection, ct);
            var name = input.Fields["index"];
            var index = result.Indexes.FirstOrDefault(i => i.Name == name);
            if (index is null)
                return result.Status is "complete" or "not-found" ? Compare(false, $"Index {name} was absent on {collection}.")
                    : Unknown($"Index metadata for {collection} is {result.Status}.");
            var changes = new List<string>();
            if (!index.Keys.SequenceEqual(ParseKeys(input.Fields["keys"])!)) changes.Add("Index keys or their order differ.");
            if (index.Unique != (input.Fields["unique"] == "yes")) changes.Add("Uniqueness differs.");
            var expectedTtl = int.TryParse(input.Fields["ttl"], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ? (long?)seconds : null;
            if (index.ExpireAfterSeconds != expectedTtl) changes.Add("Index expiry differs.");
            return Compare(changes.Count == 0, changes.Count == 0 ? "Index matches the documented definition." : "Index definition differs.",
                [$"{database}.{collection} / {name}", string.Join(", ", index.Keys.Select(k => $"{k.Field} {k.Order}")),
                 $"Unique: {index.Unique}. TTL: {index.ExpireAfterSeconds?.ToString(CultureInfo.InvariantCulture) ?? "none"}.", ..changes]);
        }
        var account = ScopedAccount(snapshot, input.AccountId, item.DataStoreId)!;
        var source = MongoDbIdentity.AuthSource(account.Parameters)!;
        var command = new Commands.ValidateMongoDbConnection(item.Endpoint, item.Username, item.Password, item.AuthenticationDatabase, item.UseTls);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            using var session = factory.Open(command);
            var identity = new BsonDocument { { "user", account.UserName }, { "db", source } };
            var users = await MongoDbInspector.Run(session, source, MongoDbInspector.UserCommand(identity, true), deadline.Token);
            var rows = users["users"].AsBsonArray;
            if (rows.Count == 0) return Compare(false, "The documented MongoDB account was not found.", $"{source} / {account.UserName}");
            if (rows.Count != 1 || rows[0]["user"] != account.UserName || rows[0]["db"] != source || !rows[0].AsBsonDocument.Contains("inheritedPrivileges"))
                return Unknown("MongoDB did not return scoped user privileges.");
            var action = input.Fields["action"];
            bool Grants(BsonValue role) => MongoDbInspector.Privileges(role, database).Any(p =>
                (p.Scope == "any-resource" || p.Scope != "cluster" && (p.Database == database || p.Database == "") &&
                    (p.Collection == "" || p.Collection == collection)) && (p.Actions.Contains(action) || p.Actions.Contains("anyAction")));
            var allowed = Grants(rows[0]);
            var details = new List<string> { $"{source} / {account.UserName} on {database}.{collection}." };
            var roles = MongoDbInspector.References(rows[0], "roles");
            if (allowed && roles.Count != 0)
            {
                try
                {
                    var requested = new BsonArray(roles.Take(32).Select(r => new BsonDocument { { "role", r.Name }, { "db", r.Database } }));
                    var definitions = await MongoDbInspector.Run(session, database, new BsonDocument { { "rolesInfo", requested }, { "showPrivileges", true } }, deadline.Token);
                    foreach (var role in definitions["roles"].AsBsonArray.Where(Grants))
                        details.Add($"Granted through {role["db"]} / {role["role"]}.");
                    if (roles.Count > 32 || definitions["roles"].AsBsonArray.Count != roles.Count)
                        details.Add("Role attribution is partial.");
                }
                catch (Exception e) when (!ct.IsCancellationRequested && MongoDbInspector.Expected(e))
                { details.Add("Role attribution: " + MongoDbInspector.Status(e) + "."); }
            }
            return Compare(allowed == (input.Fields["expected"] == "allow"), $"{action} is {(allowed ? "granted" : "not granted")}.", details.ToArray());
        }
        catch (Exception e) when (!ct.IsCancellationRequested && MongoDbInspector.Expected(e))
        { return Unknown("MongoDB privilege inspection: " + MongoDbInspector.Status(e) + "."); }
    }
}
