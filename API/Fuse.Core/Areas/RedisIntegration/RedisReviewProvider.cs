using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using StackExchange.Redis;
using static Fuse.Core.Areas.IntegrationReviews.DataStoreReview;

namespace Fuse.Core.Areas.RedisIntegration;

public sealed class RedisReviewProvider(IRedisConnectionFactory? factory = null, IntegrationPasswords? passwords = null) : IIntegrationReviewProvider
{
    private readonly IRedisConnectionFactory connections = factory ?? new RedisConnectionFactory();
    public string Provider => "redis";

    public IReadOnlyList<ReviewCheck> Checks { get; } =
    [
        new("command-access", "Redis command or channel access", [
            new("command", "Simulated command", Choices: ["GET", "SET", "DEL", "PUBLISH", "SUBSCRIBE"]),
            new("resource", "Example key or channel"),
            new("expected", "Expected result", Choices: ["allow", "deny"])], true),
        new("node-acl", "Redis ACL agreement across registered nodes", [
            new("peerIntegrationId", "Second Redis integration ID", Hint: "Choose a different registered Redis endpoint.")], true)
    ];

    public string? Validate(Snapshot snapshot, ReviewInput input)
    {
        var node = snapshot.RedisIntegrations.FirstOrDefault(i => i.Id == input.IntegrationId);
        var error = ValidateScope(snapshot, node?.DataStoreId, "Redis", input, Checks);
        if (error is not null) return error;
        if (input.Kind == "node-acl" && (!Guid.TryParse(input.Fields["peerIntegrationId"], out var peerId)
                || !snapshot.RedisIntegrations.Any(i => i.Id == peerId && i.Endpoint != node!.Endpoint)))
            return "Choose a second registered Redis integration at a different endpoint.";
        return null;
    }

    public async Task<ReviewEvidence> InspectAsync(Snapshot snapshot, ReviewInput input, CancellationToken ct)
    {
        if (Validate(snapshot, input) is { } error) return Unknown(error);
        var node = snapshot.RedisIntegrations.Single(i => i.Id == input.IntegrationId);
        var credential = await IntegrationPasswords.ForUse(node, passwords, ct);
        if (!credential.IsSuccess) return new(ReviewStatus.Unknown, IntegrationPasswords.Unavailable, []);
        node = credential.Value!;
        var username = ScopedAccount(snapshot, input.AccountId, node.DataStoreId)!.UserName!;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            if (input.Kind == "node-acl")
            {
                var peer = snapshot.RedisIntegrations.Single(i => i.Id == Guid.Parse(input.Fields["peerIntegrationId"]));
                var peerCredential = await IntegrationPasswords.ForUse(peer, passwords, deadline.Token);
                if (!peerCredential.IsSuccess) return Unknown(IntegrationPasswords.Unavailable);
                peer = peerCredential.Value!;
                var primary = await ReadUser(node, username, deadline.Token);
                var secondary = await ReadUser(peer, username, deadline.Token);
                if (primary is null || secondary is null)
                    return Compare(false, "The registered user is missing from one or both nodes.",
                        $"{node.Name}: {(primary is null ? "absent" : "present")}", $"{peer.Name}: {(secondary is null ? "absent" : "present")}");
                var changes = Differences(primary, secondary);
                return Compare(changes.Length == 0, changes.Length == 0 ? "Both registered nodes report the same ACL." : "The nodes report different ACLs.",
                    [node.Name + " ↔ " + peer.Name, ..changes]);
            }
            var command = input.Fields["command"];
            var key = input.Fields["resource"];
            object[] arguments = command is "SET" or "PUBLISH"
                ? ["DRYRUN", username, command, key, "fuse-review-example"] : ["DRYRUN", username, command, key];
            await using var session = await connections.ConnectAsync(node.Endpoint, node.Username, node.Password, deadline.Token);
            var reply = (string?)await session.ExecuteAsync("ACL", arguments, deadline.Token);
            var allowed = reply == "OK";
            if (!allowed && (reply is null || !reply.Contains("no permissions", StringComparison.OrdinalIgnoreCase)))
                return Unknown("Redis returned an unrecognized simulation result.");
            return Compare(allowed == (input.Fields["expected"] == "allow"), $"{command} on {key}: {(allowed ? "allowed" : "denied")}.",
                $"Expected {input.Fields["expected"]}.", $"Simulated for {username} on {node.Name}. Login enablement is shown in discovery.");
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is RedisException or TimeoutException or OperationCanceledException)
        {
            return Unknown("Redis could not complete the review.", RedisFailure.Message(RedisFailure.Status(e)));
        }
    }

    private async Task<RedisAclUserResponse?> ReadUser(Models.RedisIntegration node, string username, CancellationToken ct)
    {
        await using var session = await connections.ConnectAsync(node.Endpoint, node.Username, node.Password, ct);
        var result = await session.ExecuteAsync("ACL", ["GETUSER", username], ct);
        return result.IsNull ? null : RedisMetadata.ParseUser(username, result);
    }

    private static string[] Differences(RedisAclUserResponse a, RedisAclUserResponse b)
    {
        var changes = new List<string>();
        if (a.Enabled != b.Enabled || !a.Flags.Order().SequenceEqual(b.Flags.Order())) changes.Add("User flags differ.");
        if (a.Commands != b.Commands) changes.Add("Command rules differ.");
        if (a.KeyPatterns != b.KeyPatterns) changes.Add("Key patterns differ.");
        if (a.ChannelPatterns != b.ChannelPatterns) changes.Add("Channel patterns differ.");
        static IEnumerable<RedisAclSelectorResponse> Selectors(RedisAclUserResponse user) =>
            user.Selectors.OrderBy(s => s.Commands, StringComparer.Ordinal).ThenBy(s => s.KeyPatterns, StringComparer.Ordinal)
                .ThenBy(s => s.ChannelPatterns, StringComparer.Ordinal);
        if (!Selectors(a).SequenceEqual(Selectors(b))) changes.Add("ACL selectors differ.");
        return changes.ToArray();
    }
}
