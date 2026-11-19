using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Fuse.Core.Areas.MongoDbIntegration;

/// <summary>Fixed metadata commands only, with allowlisted output and explicit per-database coverage.</summary>
public sealed class MongoDbInspector(IMongoDbSessionFactory factory) : IMongoDbInspector
{
    public async Task<MongoDbIndexInventory> IndexesAsync(Models.MongoDbIntegration item, string collection, CancellationToken ct)
    {
        var database = MongoDbConnectionSettings.Database(item.Endpoint);
        var indexes = new List<MongoDbIndexDefinition>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var session = factory.Open(new(item.Endpoint, item.Username, item.Password, item.AuthenticationDatabase, item.UseTls));
            var reply = await Run(session, database, new() { { "listIndexes", collection },
                { "cursor", new BsonDocument("batchSize", Limit + 1) } }, deadline.Token);
            var cursor = reply["cursor"].AsBsonDocument;
            var id = cursor["id"].ToInt64();
            try
            {
                var batch = cursor["firstBatch"].AsBsonArray;
                foreach (var value in batch.Take(Limit))
                {
                    var row = value.AsBsonDocument;
                    var keys = row["key"].AsBsonDocument.Elements.Select(k => new MongoDbIndexField(k.Name,
                        k.Value.IsString ? k.Value.AsString : k.Value.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
                    var name = row["name"].AsString;
                    indexes.Add(new(name, keys, row.GetValue("unique", false).AsBoolean || name == "_id_",
                        row.TryGetValue("expireAfterSeconds", out var ttl) ? ttl.ToInt64() : null));
                }
                return new(collection, id != 0 || batch.Count > Limit ? "truncated" : "complete", indexes);
            }
            finally
            {
                if (id != 0) await Run(session, database, new() { { "killCursors", collection }, { "cursors", new BsonArray { id } } }, deadline.Token);
            }
        }
        catch (Exception e) when (!ct.IsCancellationRequested && Expected(e))
        { return new(collection, e is MongoCommandException { Code: 26 } ? "not-found" : Status(e), indexes); }
    }

    private const int Limit = 200, ScopeLimit = 16;
    private sealed class ResponseLimitException : Exception { }
    internal static string Status(Exception e) => e switch
    {
        MongoAuthenticationException => "authentication-failed",
        MongoCommandException { Code: 18 } => "authentication-failed",
        MongoCommandException { Code: 13 } => "denied",
        MongoCommandException { Code: 50 } => "timeout",
        TimeoutException or OperationCanceledException => "timeout",
        ResponseLimitException => "truncated",
        FormatException or InvalidCastException or KeyNotFoundException => "invalid-response",
        _ => "unavailable"
    };
    internal static bool Expected(Exception e) => e is MongoException or TimeoutException or OperationCanceledException
        or ResponseLimitException or FormatException or InvalidCastException or KeyNotFoundException;
    internal static async Task<BsonDocument> Run(IMongoDbSession session, string database, BsonDocument command, CancellationToken ct)
    {
        command["maxTimeMS"] = 5000;
        var result = await session.RunAsync(database, command, ct);
        if (result.ToBson().Length > 2 * 1024 * 1024) throw new ResponseLimitException();
        return result;
    }
    private static async Task<bool> Authenticated(IMongoDbSession session, ValidateMongoDbConnection command, CancellationToken ct)
    {
        var result = await Run(session, command.AuthenticationDatabase, new() { { "connectionStatus", 1 }, { "showPrivileges", false } }, ct);
        return result["authInfo"]["authenticatedUsers"].AsBsonArray.Any(x =>
            x["user"].AsString == command.Username && x["db"].AsString == command.AuthenticationDatabase);
    }
    public async Task<MongoDbConnectionTestResult> ValidateAsync(ValidateMongoDbConnection command, CancellationToken ct = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var session = factory.Open(command);
            var authenticated = await Authenticated(session, command, deadline.Token);
            return new(authenticated, authenticated, authenticated ? "authenticated" : "authentication-failed",
                authenticated ? null : "MongoDB did not confirm the configured identity.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (Expected(e)) { return new(false, false, Status(e), "MongoDB connection check: " + Status(e) + "."); }
    }
    public static IReadOnlyList<MongoDbAccountMatchResponse> Match(IReadOnlyList<Models.Account> accounts,
        IReadOnlyList<MongoDbUserResponse> users, IReadOnlyList<MongoDbUserScopeResponse> scopes) => accounts.Select(a =>
    {
        var source = MongoDbIdentity.AuthSource(a.Parameters);
        var found = users.Any(u => u.Username == a.UserName && u.AuthenticationDatabase == source);
        var status = string.IsNullOrEmpty(a.UserName) ? "no-username" : source is null ? "no-auth-database"
            : !MongoDbConnectionSettings.ValidDatabase(source) ? "invalid-auth-database" : found ? "matched"
            : scopes.Any(s => s.AuthenticationDatabase == source && s.Status == "complete") ? "missing" : "unknown";
        return new MongoDbAccountMatchResponse(a.Id, a.UserName, source, status);
    }).ToArray();

    public async Task<MongoDbDiscoveryResponse> DiscoverAsync(Models.MongoDbIntegration integration,
        IReadOnlyList<Models.Account> accounts, CancellationToken ct = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        var database = MongoDbConnectionSettings.Database(integration.Endpoint);
        var command = new ValidateMongoDbConnection(integration.Endpoint, integration.Username, integration.Password, integration.AuthenticationDatabase, integration.UseTls);
        var scopes = new List<MongoDbUserScopeResponse>();
        var users = new List<MongoDbUserResponse>();
        var roles = new List<MongoDbRoleResponse>();
        var collections = new List<string>();
        var collectionTypes = new Dictionary<string, string>();
        var warnings = new List<string> {
            "Users are identified by username and authentication database. Set each MongoDB account's authSource parameter explicitly; a match does not test its password.",
            "Privileges shown apply to the selected database or broader wildcard/cluster resources. Role definitions can belong to other databases. Authentication does not prove data access; no documents are read." };
        string connection = "unknown", collectionsStatus = "unknown", rolesStatus = "unknown";
        using var session = factory.Open(command);
        try
        {
            if (!await Authenticated(session, command, token)) connection = "authentication-failed";
            else
            {
                connection = "authenticated";
                collectionsStatus = await Section(async () =>
                {
                    // A nonzero cursor means the first batch is incomplete. Never interpret it as an empty database.
                    var response = await Run(session, database, new() { { "listCollections", 1 }, { "nameOnly", true },
                        { "authorizedCollections", false }, { "cursor", new BsonDocument("batchSize", Limit + 1) } }, token);
                    var cursor = response["cursor"].AsBsonDocument;
                    var id = cursor["id"].ToInt64();
                    try
                    {
                        var batch = cursor["firstBatch"].AsBsonArray;
                        foreach (var entry in batch.Take(Limit))
                        {
                            var name = entry["name"].AsString;
                            collections.Add(name);
                            collectionTypes[name] = entry.AsBsonDocument.GetValue("type", "unknown").AsString;
                        }
                        return id != 0 || batch.Count > Limit ? "truncated" : "complete";
                    }
                    finally
                    {
                        if (id != 0) await Run(session, database, new() { { "killCursors", "$cmd.listCollections" }, { "cursors", new BsonArray { id } } }, token);
                    }
                });
                rolesStatus = await Section(async () =>
                {
                    var response = await Run(session, database, new() { { "rolesInfo", 1 }, { "showBuiltinRoles", true }, { "showPrivileges", true } }, token);
                    var rows = response["roles"].AsBsonArray;
                    foreach (var row in rows.Take(Limit))
                        roles.Add(new(row["role"].AsString, row["db"].AsString, row["isBuiltin"].AsBoolean,
                            References(row, "roles"), References(row, "inheritedRoles"), Privileges(row, database)));
                    return rows.Count > Limit ? "truncated" : "complete";
                });
                var sources = new[] { database }.Concat(accounts.Select(a => MongoDbIdentity.AuthSource(a.Parameters))
                    .Where(s => MongoDbConnectionSettings.ValidDatabase(s)).Cast<string>()).Distinct(StringComparer.Ordinal).ToArray();
                if (sources.Length > ScopeLimit) warnings.Add("Only the first 16 authentication databases were inspected; other account matches are unknown.");
                foreach (var source in sources.Take(ScopeLimit))
                {
                    var remaining = Limit - users.Count;
                    if (remaining <= 0) { scopes.Add(new(source, "truncated")); continue; }
                    var status = await Section(async () =>
                    {
                        var response = await Run(session, source, UserCommand(new BsonInt32(1), false), token);
                        var rows = response["users"].AsBsonArray;
                        foreach (var row in rows.Take(remaining)) users.Add(User(row, source, database, accounts, "unknown"));
                        return rows.Count > remaining ? "truncated" : "complete";
                    });
                    scopes.Add(new(source, status));
                    // A user can inspect its own identity even without permission to enumerate other users.
                    if (status == "denied" && source == integration.AuthenticationDatabase)
                    {
                        await Section(async () =>
                        {
                            var response = await Run(session, source, UserCommand(new BsonDocument { { "user", integration.Username }, { "db", source } }, true), token);
                            foreach (var row in response["users"].AsBsonArray.Take(1)) users.Add(User(row, source, database, accounts, "complete"));
                            return "complete";
                        });
                    }
                    var observed = users.Where(u => u.AuthenticationDatabase == source && u.PrivilegesStatus == "unknown").ToArray();
                    if (observed.Length > 0)
                    {
                        var detailStatus = await Section(async () =>
                        {
                            var identities = new BsonArray(observed.Select(u => new BsonDocument { { "user", u.Username }, { "db", source } }));
                            var response = await Run(session, source, UserCommand(identities, true), token);
                            foreach (var row in response["users"].AsBsonArray.Take(remaining))
                            {
                                var user = User(row, source, database, accounts, "complete");
                                var index = users.FindIndex(u => u.Username == user.Username && u.AuthenticationDatabase == source);
                                if (index >= 0) users[index] = user;
                            }
                            return "complete";
                        });
                        if (detailStatus != "complete") warnings.Add($"Expanded user privileges in {source}: {detailStatus}.");
                        for (var j = 0; j < users.Count; j++)
                            if (users[j].AuthenticationDatabase == source && users[j].PrivilegesStatus == "unknown")
                                users[j] = users[j] with { PrivilegesStatus = detailStatus == "complete" ? "unknown" : detailStatus };
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (Expected(e)) { connection = Status(e); }
        foreach (var (section, status) in new[] { ("Connection", connection == "authenticated" ? "complete" : connection), ("Collections", collectionsStatus), ("Roles", rolesStatus) }
            .Concat(scopes.Select(s => ($"Users in {s.AuthenticationDatabase}", s.Status))))
            if (status != "complete") warnings.Add($"{section}: {status}. Unobserved entries are unknown, not absent.");
        return new(integration.Id, DateTime.UtcNow, database, connection, collectionsStatus, collections, rolesStatus, roles,
            scopes, users, Match(accounts, users, scopes), warnings) { CollectionTypes = collectionTypes };

        async Task<string> Section(Func<Task<string>> read)
        {
            try { return await read(); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception e) when (Expected(e)) { return Status(e); }
        }
    }
    internal static BsonDocument UserCommand(BsonValue users, bool privileges) => new()
        { { "usersInfo", users }, { "showCredentials", false }, { "showCustomData", false }, { "showPrivileges", privileges } };
    private static BsonArray Array(BsonValue value, string key)
    {
        var array = value.AsBsonDocument.GetValue(key, new BsonArray()).AsBsonArray;
        if (array.Count > 1000) throw new ResponseLimitException();
        return array;
    }
    internal static IReadOnlyList<MongoDbRoleReference> References(BsonValue row, string key) =>
        Array(row, key).Select(r => new MongoDbRoleReference(r["role"].AsString, r["db"].AsString)).ToArray();
    internal static IReadOnlyList<MongoDbPrivilegeResponse> Privileges(BsonValue row, string database) => Array(row, "inheritedPrivileges")
        .Where(p => p["resource"].AsBsonDocument.GetValue("anyResource", false) == true
            || p["resource"].AsBsonDocument.GetValue("cluster", false) == true
            || p["resource"].AsBsonDocument.GetValue("db", BsonNull.Value) is BsonString db && (db.AsString == database || db.AsString == ""))
        .Select(p =>
        {
            var r = p["resource"].AsBsonDocument;
            var scope = r.GetValue("anyResource", false) == true ? "any-resource" : r.GetValue("cluster", false) == true ? "cluster"
                : r["db"].AsString == "" ? "all-databases" : "database";
            return new MongoDbPrivilegeResponse(scope, r.GetValue("db", BsonNull.Value).IsBsonNull ? null : r["db"].AsString,
                r.GetValue("collection", BsonNull.Value).IsBsonNull ? null : r["collection"].AsString,
                Array(p, "actions").Select(x => x.AsString).ToArray());
        }).ToArray();
    private static MongoDbUserResponse User(BsonValue row, string expectedSource, string database, IReadOnlyList<Models.Account> accounts, string status)
    {
        var username = row["user"].AsString;
        var source = row["db"].AsString;
        if (source != expectedSource) throw new FormatException("Unexpected user scope.");
        return new(username, source, References(row, "roles"), References(row, "inheritedRoles"), Privileges(row, database), status,
            accounts.Where(a => a.UserName == username && MongoDbIdentity.AuthSource(a.Parameters) == source).Select(a => a.Id).ToArray());
    }
}
