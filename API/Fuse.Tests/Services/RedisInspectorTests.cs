using System.Text.Json;
using Fuse.Core.Areas.RedisIntegration;
using Fuse.Core.Models;
using StackExchange.Redis;
using Xunit;

namespace Fuse.Tests.Services;

public class RedisInspectorTests
{
    private static RedisResult Text(string value) => RedisResult.Create((RedisValue)value);
    private static RedisResult Array(params RedisResult[] values) => RedisResult.Create(values);
    private static RedisResult Names(params string[] values) => Array(values.Select(Text).ToArray());
    private static RedisResult User(bool enabled = true, bool legacy = false) => Array(
        Text("flags"), Names(enabled ? "on" : "off"),
        Text("passwords"), Names("private-password-hash-must-not-leak"),
        Text("commands"), Text("-@all +get +mget"),
        Text("keys"), legacy ? Names("~orders:*", "~shared:*") : Text("~orders:* ~shared:*"),
        Text("channels"), legacy ? Names("&orders:*") : Text("&orders:*"),
        Text("selectors"), Array(Array(Text("commands"), Text("-@all +set"), Text("keys"), Text("~reports:*"),
            Text("channels"), Text(""), Text("passwords"), Names("selector-hash-must-not-leak"))));

    private static readonly Guid DataStoreId = Guid.NewGuid();
    private static RedisIntegration Integration() => new(Guid.NewGuid(), "Redis", DataStoreId,
        new Uri("redis://localhost:6379"), "inspector", "private-credential", DateTime.UtcNow, DateTime.UtcNow);
    private static Account Account(string? username, Guid? targetId = null, TargetKind kind = TargetKind.DataStore) =>
        new(Guid.NewGuid(), targetId ?? DataStoreId, kind, AuthKind.UserPassword,
            new(SecretBindingKind.None, null, null), username, null, [], [], DateTime.UtcNow, DateTime.UtcNow);

    private sealed class Session(Func<string, object[], RedisResult> run) : IRedisSession
    {
        public bool Disposed { get; private set; }
        public List<(string Command, object[] Arguments)> Calls { get; } = [];
        public Task<RedisResult> ExecuteAsync(string command, object[] arguments, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add((command, arguments));
            return Task.FromResult(run(command, arguments));
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    private sealed class Factory(IRedisSession? session = null, Exception? error = null) : IRedisConnectionFactory
    {
        public Task<IRedisSession> ConnectAsync(Uri endpoint, string username, string? password, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return error is null ? Task.FromResult(session!) : Task.FromException<IRedisSession>(error);
        }
    }

    private static RedisResult Info() => Text("# Server\r\nredis_version:8.4.0\r\nredis_mode:standalone\r\nrole:master\r\n"
        + "uptime_in_seconds:123\r\nused_memory:1024\r\nconnected_clients:4\r\ndb0:keys=8,expires=2,avg_ttl=0\r\n"
        + "db3:keys=1,expires=0,avg_ttl=0\r\nconfig_file:/secret/private.conf\r\n");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MetadataProjectsPatternsAndSelectorsWithoutHashes(bool legacy)
    {
        var user = RedisMetadata.ParseUser("reader", User(legacy: legacy));
        Assert.True(user.Enabled);
        Assert.Equal("~orders:* ~shared:*", user.KeyPatterns);
        Assert.Equal("&orders:*", user.ChannelPatterns);
        Assert.Equal("~reports:*", Assert.Single(user.Selectors).KeyPatterns);
        var json = JsonSerializer.Serialize(user);
        Assert.DoesNotContain("hash-must-not-leak", json);
        Assert.DoesNotContain("passwords", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteDiscoveryMatchesExactNamesAndTargetOnly()
    {
        var session = new Session((command, args) => command == "INFO" ? Info()
            : args is ["USERS"] ? Names("reader", "disabled", "untracked")
            : User(enabled: (string)args[1] != "disabled"));
        var reader = Account("reader");
        var result = await new RedisInspector(new Factory(session)).DiscoverAsync(Integration(),
            [reader, Account("Reader"), Account("disabled"), Account("missing"), Account(null),
             Account("reader", Guid.NewGuid()), Account("reader", kind: TargetKind.Application)]);

        Assert.Equal("connected", result.ConnectionStatus);
        Assert.Equal("complete", result.AclStatus);
        Assert.True(result.AclComplete);
        Assert.Equal(5, result.Accounts.Count);
        Assert.Equal("matched", result.Accounts.Single(x => x.AccountId == reader.Id).Status);
        Assert.Equal("missing", result.Accounts.Single(x => x.Username == "Reader").Status);
        Assert.Equal("disabled", result.Accounts.Single(x => x.Username == "disabled").Status);
        Assert.Equal("missing", result.Accounts.Single(x => x.Username == "missing").Status);
        Assert.Equal("unknown", result.Accounts.Single(x => x.Username is null).Status);
        Assert.Equal("untracked", result.Users.Single(x => x.Username == "untracked").Status);
        Assert.Equal(reader.Id, Assert.Single(result.Users.Single(x => x.Username == "reader").MatchedAccountIds));
        Assert.Equal(new[] { 0, 3 }, result.Server!.Keyspaces.Select(x => x.Database));
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("private-credential", json);
        Assert.DoesNotContain("hash-must-not-leak", json);
        Assert.DoesNotContain("private.conf", json);
        Assert.True(session.Disposed);
        Assert.All(session.Calls, x => Assert.True(x.Command == "INFO" || x.Command == "ACL" && x.Arguments[0] is "USERS" or "GETUSER"));
    }

    [Fact]
    public async Task DeniedAclKeepsMetadataAndMarksAccountsUnknown()
    {
        var session = new Session((command, _) => command == "INFO" ? Info()
            : throw new RedisServerException(RedisErrorKind.Unknown, CommandFlags.None, "NOPERM private-credential private-password-hash-must-not-leak"));
        var result = await new RedisInspector(new Factory(session)).DiscoverAsync(Integration(), [Account("reader")]);
        Assert.Equal("connected", result.ConnectionStatus);
        Assert.Equal("complete", result.ServerStatus);
        Assert.Equal("denied", result.AclStatus);
        Assert.False(result.AclComplete);
        Assert.Equal("unknown", Assert.Single(result.Accounts).Status);
        Assert.DoesNotContain("private-", JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task DeniedInfoStillReturnsUsefulAclDiscovery()
    {
        var session = new Session((command, args) => command == "INFO" ? throw new RedisServerException(RedisErrorKind.Unknown, CommandFlags.None, "NOPERM INFO")
            : args is ["USERS"] ? Names("reader") : User());
        var result = await new RedisInspector(new Factory(session)).DiscoverAsync(Integration(), [Account("reader")]);
        Assert.Equal("denied", result.ServerStatus);
        Assert.Null(result.Server);
        Assert.Equal("complete", result.AclStatus);
        Assert.Equal("matched", Assert.Single(result.Accounts).Status);
    }

    [Fact]
    public async Task PartialAclDoesNotInferMissingOrDisabled()
    {
        var session = new Session((command, args) => command == "INFO" ? Info()
            : args is ["USERS"] ? Names("reader", "unknown-user")
            : (string)args[1] == "unknown-user" ? throw new RedisServerException(RedisErrorKind.Unknown, CommandFlags.None, "NOPERM GETUSER") : User());
        var result = await new RedisInspector(new Factory(session)).DiscoverAsync(Integration(),
            [Account("reader"), Account("unknown-user"), Account("missing")]);
        Assert.Equal("partial", result.AclStatus);
        Assert.False(result.AclComplete);
        Assert.Equal("matched", result.Accounts.Single(x => x.Username == "reader").Status);
        Assert.All(result.Accounts.Where(x => x.Username != "reader"), x => Assert.Equal("unknown", x.Status));
        Assert.Null(result.Users.Single(x => x.Username == "unknown-user").Enabled);
    }

    [Fact]
    public async Task LargeAclDiscoveryIsBoundedAndAbsenceRemainsUnknown()
    {
        var names = Enumerable.Range(0, RedisInspector.MaximumUsers + 1).Select(x => $"user-{x:D4}").ToArray();
        var session = new Session((command, args) => command == "INFO" ? Info() : args is ["USERS"] ? Names(names) : User());
        var result = await new RedisInspector(new Factory(session)).DiscoverAsync(Integration(), [Account(names[^1])]);
        Assert.Equal(RedisInspector.MaximumUsers, result.Users.Count);
        Assert.Equal(RedisInspector.MaximumUsers, session.Calls.Count(x => x.Arguments is ["GETUSER", _]));
        Assert.Equal("partial", result.AclStatus);
        Assert.Equal("unknown", Assert.Single(result.Accounts).Status);
    }

    [Theory]
    [InlineData("WRONGPASS private-credential", "authentication-failed")]
    [InlineData("NOAUTH private-credential", "authentication-failed")]
    [InlineData("NOPERM private-credential", "permission-denied")]
    [InlineData("connection failed private-credential", "unavailable")]
    public async Task ConnectionErrorsNeverExposeCredentialsOrRawErrors(string error, string expected)
    {
        var inspector = new RedisInspector(new Factory(error: new RedisServerException(RedisErrorKind.Unknown, CommandFlags.None, error)));
        var validation = await inspector.ValidateAsync(Integration().Endpoint, "inspector", "private-credential");
        Assert.False(validation.IsSuccessful);
        Assert.False(validation.Authenticated);
        Assert.Equal(expected, validation.Status);
        Assert.DoesNotContain("private-credential", JsonSerializer.Serialize(validation));
        var discovery = await inspector.DiscoverAsync(Integration(), [Account("reader")]);
        Assert.Equal(expected, discovery.ConnectionStatus);
        Assert.Equal("unknown", Assert.Single(discovery.Accounts).Status);
        Assert.DoesNotContain("private-credential", JsonSerializer.Serialize(discovery));
    }

    [Fact]
    public async Task AuthenticatedUserWithoutPingIsDistinctFromAuthenticationFailure()
    {
        var session = new Session((_, _) => throw new RedisServerException(RedisErrorKind.Unknown, CommandFlags.None, "NOPERM PING"));
        var result = await new RedisInspector(new Factory(session)).ValidateAsync(Integration().Endpoint, "inspector", "password");
        Assert.True(result.Authenticated);
        Assert.False(result.IsSuccessful);
        Assert.Equal("permission-denied", result.Status);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task TimeoutIsSafeAndCallerCancellationIsPropagated()
    {
        var inspector = new RedisInspector(new Factory(error: new TimeoutException("private-credential")));
        var result = await inspector.ValidateAsync(Integration().Endpoint, "inspector", "password");
        Assert.Equal("timeout", result.Status);
        Assert.DoesNotContain("private-credential", result.ErrorMessage);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inspector.ValidateAsync(Integration().Endpoint, "inspector", "password", canceled.Token));
    }

    [Fact]
    public void EmptyPasswordStillHasExplicitIdentityAndTlsUsesCertificateValidation()
    {
        var options = RedisConnectionFactory.Options(new Uri("rediss://redis.example:6380"), "nopass-user", "");
        Assert.Equal("nopass-user", options.User);
        Assert.Equal("", options.Password);
        Assert.True(options.Ssl);
        Assert.Equal("redis.example", options.SslHost);
        Assert.Equal(RedisProtocol.Resp2, options.Protocol);
        Assert.Equal("", options.TieBreaker);
        Assert.Equal("", options.ConfigurationChannel);
        Assert.Equal("default", RedisConnectionFactory.Options(new Uri("redis://localhost"), "", null).User);
    }

    [Theory]
    [InlineData("OK", "allow", "Matched")]
    [InlineData("This user has no permissions to run the 'set' command", "deny", "Matched")]
    [InlineData("This user has no permissions to run the 'set' command", "allow", "Drift")]
    [InlineData("unexpected result", "deny", "Unknown")]
    [InlineData("inspector-denied", "deny", "Unknown")]
    public async Task ReviewSimulatesWriteAndSeparatesInspectorDenial(string reply, string expected, string status)
    {
        var node = Integration();
        var account = Account("reader");
        var store = new DataStore(DataStoreId, "Redis", null, "Redis", Guid.NewGuid(), null, node.Endpoint, [], DateTime.UtcNow, DateTime.UtcNow);
        var snapshot = new Fuse.Tests.TestInfrastructure.InMemoryFuseStore().Current! with { RedisIntegrations = [node], Accounts = [account], DataStores = [store] };
        var session = new Session((_, _) => reply == "inspector-denied"
            ? throw new RedisServerException(RedisErrorKind.Unknown, CommandFlags.None, "NOPERM private-password")
            : Text(reply));
        var provider = new RedisReviewProvider(new Factory(session));
        var input = new Fuse.Core.Areas.IntegrationReviews.ReviewInput("redis", node.Id, "command-access", account.Id,
            new() { ["command"] = "SET", ["resource"] = "orders:example", ["expected"] = expected });
        var result = await provider.InspectAsync(snapshot, input, default);
        Assert.Equal(status, result.Status.ToString());
        var call = Assert.Single(session.Calls);
        Assert.Equal("ACL", call.Command);
        Assert.Equal(new object[] { "DRYRUN", "reader", "SET", "orders:example", "fuse-review-example" }, call.Arguments);
        Assert.DoesNotContain("private-password", JsonSerializer.Serialize(result));
        Assert.NotNull(provider.Validate(snapshot, input with { AccountId = Guid.NewGuid() }));
    }
}
