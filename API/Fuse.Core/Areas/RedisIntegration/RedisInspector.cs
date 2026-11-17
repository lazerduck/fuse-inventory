using System.Globalization;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using StackExchange.Redis;

namespace Fuse.Core.Areas.RedisIntegration;

/// <summary>Only fixed metadata commands are used. No key scans, key reads or ACL mutations.</summary>
public sealed class RedisInspector(IRedisConnectionFactory? connectionFactory = null) : IRedisInspector
{
    public const int MaximumUsers = 256;
    private readonly IRedisConnectionFactory _connections = connectionFactory ?? new RedisConnectionFactory();

    public async Task<RedisConnectionTestResult> ValidateAsync(Uri endpoint, string username, string? password, CancellationToken ct = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var authenticated = false;
        try
        {
            await using var session = await _connections.ConnectAsync(endpoint, username, password, deadline.Token);
            authenticated = true;
            await session.ExecuteAsync("PING", [], deadline.Token);
            return new(true, true, "connected", null);
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            var status = RedisFailure.Status(error);
            return new(false, authenticated, status, RedisFailure.Message(status));
        }
    }

    public async Task<RedisDiscoveryResponse> DiscoverAsync(Models.RedisIntegration integration,
        IReadOnlyList<Models.Account> accounts, CancellationToken ct = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var observedAt = DateTime.UtcNow;
        var users = new List<RedisAclUserResponse>();
        var warnings = new List<string>();
        RedisServerInfo? server = null;
        var serverStatus = "unavailable";
        var aclStatus = "unavailable";
        var connectionStatus = "unavailable";
        var complete = false;
        try
        {
            await using var session = await _connections.ConnectAsync(integration.Endpoint, integration.Username, integration.Password, deadline.Token);
            connectionStatus = "connected";
            try
            {
                var info = await session.ExecuteAsync("INFO", [], deadline.Token);
                server = RedisMetadata.ParseInfo((string?)info ?? "");
                serverStatus = "complete";
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                serverStatus = SectionStatus(error);
                warnings.Add("Server metadata could not be read. " + RedisFailure.Message(RedisFailure.Status(error)));
            }
            try
            {
                var result = await session.ExecuteAsync("ACL", ["USERS"], deadline.Token);
                var names = RedisMetadata.Usernames(result);
                complete = names.Count <= MaximumUsers;
                if (!complete) warnings.Add($"ACL discovery is limited to {MaximumUsers} users; unmatched accounts are unknown.");
                foreach (var name in names.Take(MaximumUsers))
                {
                    try
                    {
                        var raw = await session.ExecuteAsync("ACL", ["GETUSER", name], deadline.Token);
                        users.Add(RedisMetadata.ParseUser(name, raw));
                    }
                    catch (Exception) when (!ct.IsCancellationRequested)
                    {
                        complete = false;
                        users.Add(new(name, null, [], null, null, null, [], [], "unknown"));
                        if (warnings.All(x => !x.StartsWith("Some ACL user", StringComparison.Ordinal)))
                            warnings.Add("Some ACL user details could not be read; their permissions and account state are unknown.");
                        if (deadline.IsCancellationRequested) break;
                    }
                }
                aclStatus = complete ? "complete" : "partial";
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                complete = false;
                aclStatus = SectionStatus(error);
                warnings.Add("ACL discovery could not be completed. " + RedisFailure.Message(RedisFailure.Status(error)));
            }
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            connectionStatus = RedisFailure.Status(error);
            warnings.Add(RedisFailure.Message(connectionStatus));
        }
        var scoped = accounts.Where(x => x.TargetKind == TargetKind.DataStore && x.TargetId == integration.DataStoreId).ToArray();
        var matched = RedisMetadata.MatchAccounts(users, scoped, complete);
        return new(integration.Id, observedAt, connectionStatus, serverStatus, aclStatus, complete,
            server, matched.Users, matched.Accounts, warnings);
    }

    private static string SectionStatus(Exception error) => RedisFailure.Status(error) switch
    {
        "permission-denied" => "denied",
        "timeout" => "timeout",
        "unsupported" => "unsupported",
        _ => "unavailable"
    };
}

public interface IRedisSession : IAsyncDisposable
{
    Task<RedisResult> ExecuteAsync(string command, object[] arguments, CancellationToken ct);
}

public interface IRedisConnectionFactory
{
    Task<IRedisSession> ConnectAsync(Uri endpoint, string username, string? password, CancellationToken ct);
}

public sealed class RedisConnectionFactory : IRedisConnectionFactory
{
    public static ConfigurationOptions Options(Uri endpoint, string username, string? password)
    {
        var validation = RedisConnectionSettings.Validate(endpoint, username, password);
        if (!validation.IsSuccess) throw new ArgumentException(validation.Error);
        var options = new ConfigurationOptions
        {
            // A username always triggers AUTH, including an empty password for nopass users.
            User = RedisConnectionSettings.Username(username), Password = password ?? "",
            Ssl = endpoint.Scheme == "rediss", SslHost = endpoint.Host,
            AbortOnConnectFail = true, ConnectRetry = 0, ConnectTimeout = 4000,
            AsyncTimeout = 4000, SyncTimeout = 4000, AllowAdmin = true,
            Protocol = RedisProtocol.Resp2, SetClientLibrary = false,
            TieBreaker = "", ConfigurationChannel = "", BacklogPolicy = BacklogPolicy.FailFast,
            // In particular, exclude SET/GET (client tie-breaker probes), CONFIG,
            // cluster topology expansion and subscriptions. ACL calls below are
            // restricted to USERS/GETUSER; no ACL LIST password hashes are used.
            CommandMap = CommandMap.Create(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "AUTH", "ECHO", "PING", "INFO", "ACL", "QUIT" }, available: true)
        };
        options.EndPoints.Add(endpoint.Host, endpoint.Port > 0 ? endpoint.Port : 6379);
        return options;
    }

    public async Task<IRedisSession> ConnectAsync(Uri endpoint, string username, string? password, CancellationToken ct)
    {
        var task = ConnectionMultiplexer.ConnectAsync(Options(endpoint, username, password));
        ConnectionMultiplexer connection;
        try { connection = await task.WaitAsync(ct); }
        catch
        {
            // A cancelled request must not strand a later successful connection.
            _ = task.ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion) t.Result.Dispose(); else _ = t.Exception; },
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
        try
        {
            return new RedisSession(connection, connection.GetServer(endpoint.Host, endpoint.Port > 0 ? endpoint.Port : 6379));
        }
        catch { connection.Dispose(); throw; }
    }

    private sealed class RedisSession(ConnectionMultiplexer connection, IServer server) : IRedisSession
    {
        public Task<RedisResult> ExecuteAsync(string command, object[] arguments, CancellationToken ct)
        {
            if (!(command == "PING" && arguments.Length == 0 || command == "INFO" && arguments.Length == 0
                  || command == "ACL" && (arguments is ["USERS"] || arguments is ["GETUSER", string])))
                throw new InvalidOperationException("Only fixed Redis metadata commands are supported.");
            return server.ExecuteAsync(command, arguments).WaitAsync(ct);
        }
        public ValueTask DisposeAsync() { connection.Dispose(); return ValueTask.CompletedTask; }
    }
}

/// <summary>Exception text is classified privately and never returned or logged.</summary>
public static class RedisFailure
{
    public static string Status(Exception error)
    {
        var errors = new List<Exception>();
        for (var item = error; item is not null && errors.Count < 8; item = item.InnerException) errors.Add(item);
        if (errors.Any(x => x.Message.Contains("NOPERM", StringComparison.OrdinalIgnoreCase))) return "permission-denied";
        if (errors.Any(x => x.Message.Contains("WRONGPASS", StringComparison.OrdinalIgnoreCase)
            || x.Message.Contains("NOAUTH", StringComparison.OrdinalIgnoreCase)
            || x is RedisConnectionException { FailureType: ConnectionFailureType.AuthenticationFailure })) return "authentication-failed";
        if (errors.Any(x => x is TimeoutException or OperationCanceledException or RedisTimeoutException)) return "timeout";
        if (errors.Any(x => x is RedisServerException && x.Message.Contains("unknown command", StringComparison.OrdinalIgnoreCase))) return "unsupported";
        return "unavailable";
    }

    public static string Message(string status) => status switch
    {
        "authentication-failed" => "Redis authentication failed. Check the username/password and whether the user is enabled.",
        "permission-denied" => "The Redis user does not permit this inspection command.",
        "timeout" => "Redis inspection timed out. Check the endpoint and network reachability.",
        "unsupported" => "This Redis endpoint does not support the requested metadata command.",
        _ => "Redis could not be inspected. Check the endpoint, TLS configuration and network reachability."
    };
}

/// <summary>Projection deliberately ignores the password field and unrecognized server metadata.</summary>
public static class RedisMetadata
{
    private static Dictionary<string, RedisResult> Fields(RedisResult result)
    {
        var values = (RedisResult[]?)result ?? throw new FormatException("Metadata is unavailable.");
        if (values.Length % 2 != 0) throw new FormatException("Metadata is incomplete.");
        var fields = new Dictionary<string, RedisResult>(StringComparer.Ordinal);
        for (var i = 0; i < values.Length; i += 2)
        {
            var key = (string?)values[i] ?? throw new FormatException("Metadata is incomplete.");
            // The password/hash array is deliberately not retained in the projection.
            if (key is "flags" or "commands" or "keys" or "channels" or "selectors") fields[key] = values[i + 1];
        }
        return fields;
    }
    private static string? Text(IReadOnlyDictionary<string, RedisResult> fields, string key)
    {
        if (!fields.TryGetValue(key, out var value) || value.IsNull) return null;
        // Redis 6 returns key/channel patterns as arrays; Redis 7+ returns strings.
        if (value.Resp2Type == ResultType.Array)
            return string.Join(" ", ((RedisResult[]?)value ?? []).Select(x => (string?)x));
        return (string?)value;
    }
    public static IReadOnlyList<string> Usernames(RedisResult result) =>
        ((RedisResult[]?)result ?? throw new FormatException("User listing is unavailable."))
        .Select(x => (string?)x ?? throw new FormatException("Invalid user listing."))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    public static RedisAclUserResponse ParseUser(string username, RedisResult result)
    {
        if (result.IsNull) throw new FormatException("User disappeared during discovery.");
        var fields = Fields(result);
        var flags = fields.TryGetValue("flags", out var f) ? Usernames(f) : throw new FormatException("User flags are unavailable.");
        var commands = Text(fields, "commands") ?? throw new FormatException("Command rules are unavailable.");
        var selectors = fields.TryGetValue("selectors", out var raw) && !raw.IsNull
            ? ((RedisResult[]?)raw ?? []).Select(x =>
            {
                var selector = Fields(x);
                return new RedisAclSelectorResponse(Text(selector, "commands"), Text(selector, "keys"), Text(selector, "channels"));
            }).ToArray() : [];
        bool? enabled = flags.Contains("on", StringComparer.Ordinal) ? true : flags.Contains("off", StringComparer.Ordinal) ? false : null;
        if (enabled is null) throw new FormatException("User enablement is unavailable.");
        return new(username, enabled, flags, commands, Text(fields, "keys"), Text(fields, "channels"), selectors, [], enabled.Value ? "untracked" : "disabled");
    }

    public static RedisServerInfo ParseInfo(string text)
    {
        var fields = text.Split('\n').Select(x => x.TrimEnd('\r')).Where(x => x.Length > 0 && x[0] != '#')
            .Select(x => x.Split(':', 2)).Where(x => x.Length == 2).GroupBy(x => x[0], StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last()[1], StringComparer.Ordinal);
        if (fields.Count == 0) throw new FormatException("Server metadata is unavailable.");
        string? Get(string key) => fields.GetValueOrDefault(key);
        long? Number(string key) => long.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= 0 ? number : null;
        var keyspaces = new List<RedisKeyspaceInfo>();
        foreach (var (key, value) in fields)
        {
            if (!key.StartsWith("db", StringComparison.Ordinal) || !int.TryParse(key.AsSpan(2), out var database) || database < 0) continue;
            var counts = value.Split(',').Select(x => x.Split('=', 2)).Where(x => x.Length == 2).ToDictionary(x => x[0], x => x[1]);
            if (long.TryParse(counts.GetValueOrDefault("keys"), out var keys) && long.TryParse(counts.GetValueOrDefault("expires"), out var expires)
                && keys >= 0 && expires >= 0) keyspaces.Add(new(database, keys, expires));
        }
        return new(Get("redis_version"), Get("redis_mode"), Get("role"), Number("uptime_in_seconds"),
            Number("used_memory"), Number("connected_clients"), keyspaces.OrderBy(x => x.Database).ToArray());
    }

    public static (IReadOnlyList<RedisAclUserResponse> Users, IReadOnlyList<RedisAccountMatchResponse> Accounts)
        MatchAccounts(IReadOnlyList<RedisAclUserResponse> users, IReadOnlyList<Models.Account> accounts, bool complete)
    {
        var map = users.ToDictionary(x => x.Username, StringComparer.Ordinal);
        var matches = accounts.Select(account =>
        {
            var found = account.UserName is not null && map.TryGetValue(account.UserName, out var user) ? user : null;
            var status = found is null ? complete && !string.IsNullOrEmpty(account.UserName) ? "missing" : "unknown"
                : found.Enabled is null ? "unknown" : found.Enabled.Value ? "matched" : "disabled";
            return new RedisAccountMatchResponse(account.Id, account.UserName, status, found?.Username);
        }).ToArray();
        var projected = users.Select(user =>
        {
            var ids = matches.Where(x => string.Equals(x.RedisUsername, user.Username, StringComparison.Ordinal)).Select(x => x.AccountId).ToArray();
            return user with { MatchedAccountIds = ids, Status = user.Enabled is null ? "unknown" : !user.Enabled.Value ? "disabled" : ids.Length == 0 ? "untracked" : "matched" };
        }).ToArray();
        return (projected, matches);
    }
}
