using Fuse.Core.Models;

namespace Fuse.Core.Areas.IntegrationReviews;

public sealed record SaveReview(string Name, ReviewInput Input, int RefreshMinutes = 0,
    Guid[]? ApplicationInstanceIds = null, Guid? ExpectedRevision = null);
public sealed record ReviewAttempt(DateTimeOffset CheckedAt, ReviewEvidence Evidence);
public sealed record ReviewDefinition(Guid Id, Guid Revision, string Name, ReviewInput Input, int RefreshMinutes,
    Guid[] ApplicationInstanceIds, ReviewAttempt? LastAttempt, ReviewAttempt? LastVerified, IReadOnlyList<ReviewAttempt> History, string? ScopeFingerprint = null);
public sealed record ReviewSource(string Provider, Guid Id, string Name, Guid? TargetId, DateTime UpdatedAt);
public sealed record ReviewApplication(Guid ApplicationId, Guid InstanceId, string Name, string? Environment, string? Owner);
public sealed record ReviewCatalog(string Provider, IReadOnlyList<ReviewCheck> Checks, IReadOnlyList<ReviewSource> Sources);
public sealed record ReviewView(ReviewDefinition Review, IReadOnlyList<ReviewApplication> Applications);

public static class ReviewSources
{
    public static IEnumerable<ReviewSource> All(Snapshot s) =>
        s.PostgreSqlIntegrations.Select(x => new ReviewSource("postgresql", x.Id, x.Name, x.DataStoreId, x.UpdatedAt))
        .Concat(s.RedisIntegrations.Select(x => new ReviewSource("redis", x.Id, x.Name, x.DataStoreId, x.UpdatedAt)))
        .Concat(s.MongoDbIntegrations.Select(x => new ReviewSource("mongodb", x.Id, x.Name, x.DataStoreId, x.UpdatedAt)))
        .Concat(s.RabbitMqIntegrations.Select(x => new ReviewSource("rabbitmq", x.Id, x.Name, x.MessageBrokerId, x.UpdatedAt)))
        .Concat(s.KeycloakIntegrations.Select(x => new ReviewSource("keycloak", x.Id, x.Name, null, x.UpdatedAt)))
        .Concat(s.ServiceBusIntegrations.Select(x => new ReviewSource("servicebus", x.Id, x.Name, x.MessageBrokerId, x.UpdatedAt)))
        .Concat(s.CosmosDbIntegrations.Select(x => new ReviewSource("cosmosdb", x.Id, x.Name, x.DataStoreId, x.UpdatedAt)))
        .Concat(s.SqlIntegrations.Select(x => new ReviewSource("sql", x.Id, x.Name, x.DataStoreId, x.UpdatedAt)));

    // Bind evidence to provider scope, also when an existing connection or account is edited.
    public static string Scope(Snapshot s, ReviewInput input)
    {
        var id = input.IntegrationId;
        string?[] resource = input.Provider switch
        {
            "postgresql" when s.PostgreSqlIntegrations.FirstOrDefault(x => x.Id == id) is {} p => [p.Endpoint.AbsoluteUri],
            "redis" when s.RedisIntegrations.FirstOrDefault(x => x.Id == id) is {} r => [r.Endpoint.AbsoluteUri],
            "mongodb" when s.MongoDbIntegrations.FirstOrDefault(x => x.Id == id) is {} m => [m.Endpoint.AbsoluteUri],
            "rabbitmq" when s.RabbitMqIntegrations.FirstOrDefault(x => x.Id == id) is {} b => [b.Endpoint.AbsoluteUri, b.VirtualHost],
            "keycloak" when s.KeycloakIntegrations.FirstOrDefault(x => x.Id == id) is {} k => [k.Issuer.AbsoluteUri],
            "cosmosdb" when s.CosmosDbIntegrations.FirstOrDefault(x => x.Id == id) is {} c => [c.AccountResourceId],
            "servicebus" when s.ServiceBusIntegrations.FirstOrDefault(x => x.Id == id) is {} ns => [ns.NamespaceResourceId],
            "sql" when s.SqlIntegrations.FirstOrDefault(x => x.Id == id) is {} sql => SqlScope(sql),
            _ => []
        };
        if (input.Provider == "redis" && input.Kind == "node-acl" && Guid.TryParse(input.Fields.GetValueOrDefault("peerIntegrationId"), out var peerId))
        {
            var peer = s.RedisIntegrations.FirstOrDefault(r => r.Id == peerId);
            resource = [..resource, peer?.DataStoreId.ToString(), peer?.Endpoint.AbsoluteUri];
        }
        var account = s.Accounts.FirstOrDefault(x => x.Id == input.AccountId);
        string?[] scope = [..resource, Find(s, input)?.TargetId?.ToString(), account?.TargetId.ToString(), account?.UserName,
            account?.Parameters?.GetValueOrDefault("authSource"), account?.Parameters?.GetValueOrDefault("keycloakIssuer"),
            account?.Parameters?.GetValueOrDefault("keycloakClientId")];
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(scope)));
    }
    private static string?[] SqlScope(Models.SqlIntegration sql)
    {
        try
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql.ConnectionString);
            return [builder.DataSource, builder.InitialCatalog];
        }
        catch (ArgumentException) { return []; }
    }

    public static ReviewSource? Find(Snapshot snapshot, ReviewInput input) =>
        All(snapshot).FirstOrDefault(x => x.Id == input.IntegrationId && x.Provider == input.Provider);

    public static IReadOnlyList<ReviewApplication> Applications(Snapshot s, ReviewDefinition review)
    {
        var target = Find(s, review.Input)?.TargetId;
        var account = s.Accounts.FirstOrDefault(x => x.Id == review.Input.AccountId);
        return (from app in s.Applications
                from instance in app.Instances
                where review.ApplicationInstanceIds.Contains(instance.Id)
                    || (account?.TargetKind == TargetKind.Application && (account.TargetId == app.Id || account.TargetId == instance.Id))
                    || instance.Dependencies.Any(d => account is not null ? d.AccountId == account.Id
                        : review.ApplicationInstanceIds.Length == 0 && target.HasValue && d.TargetId == target.Value)
                select new ReviewApplication(app.Id, instance.Id, app.Name,
                    s.Environments.FirstOrDefault(e => e.Id == instance.EnvironmentId)?.Name, app.Owner)).ToArray();
    }
}
