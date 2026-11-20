using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.IntegrationReviews;

public sealed record SaveReview(string Name, ReviewInput Input, int RefreshMinutes = 0,
    Guid[]? ApplicationInstanceIds = null, Guid? ExpectedRevision = null);
public sealed record ReviewAttempt(DateTimeOffset CheckedAt, ReviewEvidence Evidence);
public sealed record ReviewDefinition(Guid Id, Guid Revision, string Name, ReviewInput Input, int RefreshMinutes,
    Guid[] ApplicationInstanceIds, ReviewAttempt? LastAttempt, ReviewAttempt? LastVerified, IReadOnlyList<ReviewAttempt> History, string? ScopeFingerprint = null);
public sealed record ReviewSource(string Provider, Guid Id, string Name, Guid? TargetId, DateTime UpdatedAt);
public sealed record ReviewApplication(Guid ApplicationId, Guid InstanceId, string Name, string? Environment, string? Owner, IReadOnlyList<string>? Responsibilities = null);
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
        if (input.Provider == "mongodb" && input.Kind == "index-agreement" && Guid.TryParse(input.Fields.GetValueOrDefault("peerIntegrationId"), out var mongoPeerId))
        {
            var peer = s.MongoDbIntegrations.FirstOrDefault(m => m.Id == mongoPeerId);
            resource = [..resource, peer?.DataStoreId.ToString(), peer?.Endpoint.AbsoluteUri];
        }
        var account = s.Accounts.FirstOrDefault(x => x.Id == input.AccountId);
        if (Guid.TryParse(input.Fields.GetValueOrDefault("identityId"), out var identityId))
        {
            var principal = s.Identities.FirstOrDefault(i => i.Id == identityId)?.AzurePrincipal;
            resource = [..resource, principal?.TenantId.ToString(), principal?.ObjectId.ToString()];
        }
        if (input.Provider == "sql" && account is not null)
        {
            var grants = account.Grants.Where(g => SqlPermissionDiff.IsDatabaseScope(g.Database, g.Schema))
                .Select(g => System.Text.Json.JsonSerializer.Serialize(
                new { g.Database, g.Schema, Privileges = g.Privileges.Order().ToArray() })).Order(StringComparer.Ordinal);
            // Observations made by the former database-and-schema comparison used a different scope.
            resource = [..resource, "whole-database-grants-v1", ..grants];
        }
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

    public static IReadOnlyList<ReviewApplication> Applications(Snapshot s, ReviewDefinition review,
        bool includeEnvironment = false, bool includeResponsibilities = false)
    {
        var target = Find(s, review.Input)?.TargetId;
        var account = s.Accounts.FirstOrDefault(x => x.Id == review.Input.AccountId);
        var identity = Guid.TryParse(review.Input.Fields.GetValueOrDefault("identityId"), out var identityId)
            ? s.Identities.FirstOrDefault(i => i.Id == identityId) : null;
        return (from app in s.Applications
                from instance in app.Instances
                where review.ApplicationInstanceIds.Contains(instance.Id)
                    || (account?.TargetKind == TargetKind.Application && (account.TargetId == app.Id || account.TargetId == instance.Id))
                    || identity?.OwnerInstanceId == instance.Id
                    || instance.Dependencies.Any(d => identity is not null ? d.IdentityId == identity.Id
                        : account is not null ? d.AccountId == account.Id
                        : review.ApplicationInstanceIds.Length == 0 && target.HasValue && d.TargetId == target.Value)
                select new ReviewApplication(app.Id, instance.Id, app.Name,
                    includeEnvironment ? s.Environments.FirstOrDefault(e => e.Id == instance.EnvironmentId)?.Name : null,
                    app.Owner, includeResponsibilities ? (from assignment in s.ResponsibilityAssignments
                        where assignment.ApplicationId == app.Id && (assignment.Scope == ResponsibilityScope.All ||
                            assignment.EnvironmentId == instance.EnvironmentId)
                        join position in s.Positions on assignment.PositionId equals position.Id
                        join type in s.ResponsibilityTypes on assignment.ResponsibilityTypeId equals type.Id
                        orderby assignment.Primary descending, type.Name, position.Name
                        select type.Name + " · " + position.Name).Distinct().ToArray() : null)).ToArray();
    }
}
