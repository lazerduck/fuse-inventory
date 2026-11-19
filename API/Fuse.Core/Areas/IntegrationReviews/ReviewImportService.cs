using Fuse.Core.Areas.Audit;
using Fuse.Core.Areas.KeycloakIntegration;
using Fuse.Core.Areas.MongoDbIntegration;
using Fuse.Core.Areas.PostgreSqlIntegration;
using Fuse.Core.Areas.RabbitMqIntegration;
using Fuse.Core.Areas.RedisIntegration;
using Fuse.Core.Areas.ServiceBusIntegration;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.IntegrationReviews;

public sealed record ObservedItem(string Kind, string Name, string Scope, string Coverage = "observed", string? NativeId = null);
public sealed record ImportSelection(string Provider, Guid IntegrationId, ObservedItem Item, Guid? InstanceId = null);
public sealed record ImportPreview(ImportSelection Selection, DateTime ConnectionVersion, Guid? ExistingId, string Destination, bool LinkDependency,
    Guid DestinationId, string ResourceFingerprint, bool RecordOrigin, string? Application, Guid? ApplicationId, Guid? EnvironmentId, string? ConfirmationToken = null);
public sealed record ImportReceipt(Guid InventoryId, bool Created, bool Linked);

public sealed class ReviewImportService(IFuseStore store, IAuditService audit, ICurrentUser user,
    IPostgreSqlIntegrationService postgres, IRedisIntegrationService redis, IMongoDbIntegrationService mongo,
    IRabbitMqIntegrationService rabbit, IKeycloakIntegrationService keycloak, IServiceBusIntegrationService bus)
{
    public async Task<IReadOnlyList<ObservedItem>> DiscoverAsync(string provider, Guid id, CancellationToken ct)
    {
        IEnumerable<ObservedItem>? items = provider switch
        {
            "postgresql" => (await postgres.DiscoverAsync(id, ct)).Value is {} postgresData ? postgresData.Roles.Where(r => r.CanLogin).Select(r => new ObservedItem("account", r.Name, "cluster", Coverage(postgresData.RolesStatus))) : [],
            "redis" => (await redis.DiscoverAsync(id, ct)).Value is {} redisData ? redisData.Users.Select(r => new ObservedItem("account", r.Username, "server", Coverage(redisData.AclStatus))) : [],
            "mongodb" => (await mongo.DiscoverAsync(id, ct)).Value is {} mongoData ? mongoData.Users.Select(r => new ObservedItem("account", r.Username, r.AuthenticationDatabase, Coverage(mongoData.UserScopes.FirstOrDefault(u => u.AuthenticationDatabase == r.AuthenticationDatabase)?.Status))) : [],
            "servicebus" => (await bus.DiscoverAsync(id, ct)).Value is {} busData ? busData.Queues.Select(q => new ObservedItem("queue", q.Name, "namespace", Coverage(busData.QueueVisibility))) : [],
            "rabbitmq" => RabbitItems((await rabbit.DiscoverAsync(id, ct)).Value),
            "keycloak" => ClientItems((await keycloak.DiscoverAsync(id, ct)).Value),
            _ => []
        };
        return (items ?? []).Distinct().OrderBy(i => i.Kind).ThenBy(i => i.Name, StringComparer.Ordinal).ThenBy(i => i.Scope).ToArray();
    }

    private static string Coverage(string? status) => status is "complete" or "visible" ? "complete" : "limited";
    private static IEnumerable<ObservedItem> RabbitItems(Responses.RabbitMqDiscoveryResponse? d) =>
        d is null ? [] : d.Users.Select(u => new ObservedItem("account", u, "broker", Coverage(d.UsersStatus)))
            .Concat(d.Resources.Where(q => q.Kind == "queues").Select(q => new ObservedItem("queue", q.Name, d.VirtualHost, Coverage(d.TopologyStatus))));
    private static IEnumerable<ObservedItem> ClientItems(Responses.KeycloakDiscoveryResponse? d) =>
        d is null ? [] : d.Clients.Select(r => new ObservedItem("client", r.ClientId, d.Issuer.AbsoluteUri.TrimEnd('/'), Coverage(d.ClientsStatus), r.Id));

    private static ReviewSource? Source(Snapshot s, ImportSelection selection) =>
        ReviewSources.All(s).FirstOrDefault(x => x.Provider == selection.Provider && x.Id == selection.IntegrationId);

    public async Task<Result<ImportPreview>> PreviewAsync(ImportSelection selection, CancellationToken ct)
    {
        if (selection.Item is null || string.IsNullOrWhiteSpace(selection.Item.Name))
            return Result<ImportPreview>.Failure("Select an observed item.");
        var source = Source(await store.GetAsync(ct), selection);
        if (source is null) return Result<ImportPreview>.Failure("Connection not found.", ErrorType.NotFound);
        var visible = await DiscoverAsync(selection.Provider, selection.IntegrationId, ct);
        var observed = visible.FirstOrDefault(i => i.Kind == selection.Item.Kind && i.Name == selection.Item.Name && i.Scope == selection.Item.Scope);
        if (observed is null || selection.Item.NativeId is not null && observed.NativeId != selection.Item.NativeId) return Result<ImportPreview>.Failure("The selected item is no longer visible. Inspect again.", ErrorType.Conflict);
        var s = await store.GetAsync(ct);
        if (Source(s, selection) != source) return Result<ImportPreview>.Failure("The connection changed during discovery.", ErrorType.Conflict);
        return Prepare(s, selection with { Item = observed }, source);
    }

    private static Result<ImportPreview> Prepare(Snapshot s, ImportSelection selection, ReviewSource source)
    {
        var item = selection.Item;
        var instance = selection.InstanceId is null ? null : s.Applications.SelectMany(a => a.Instances).FirstOrDefault(i => i.Id == selection.InstanceId);
        if (selection.InstanceId.HasValue && instance is null || item.Kind == "client" && instance is null)
            return Result<ImportPreview>.Failure("Select an existing application instance.");
        var target = item.Kind == "client" ? instance?.Id : source.TargetId;
        if (target is null) return Result<ImportPreview>.Failure("No inventory target is available.");
        if (item.Kind == "queue" && s.MessageBrokers.All(b => b.Id != target))
            return Result<ImportPreview>.Failure("Message broker no longer exists.", ErrorType.Conflict);
        var existing = Existing(s, selection, target.Value);
        if (existing.Length > 1) return Result<ImportPreview>.Failure("Several inventory accounts match this identity. Resolve the duplicates before importing.", ErrorType.Conflict);
        var origin = Origin(s, selection);
        var needsOrigin = false;
        if (existing.Length == 1)
        {
            var prior = item.Kind == "queue" ? s.MessageBrokers.Single(b => b.Id == target).Queues?.First(q => q.Id == existing[0]).ImportedFrom
                : s.Accounts.Single(a => a.Id == existing[0]).ImportedFrom;
            needsOrigin = prior is null;
            if (prior is not null && (prior.Provider != origin.Provider || prior.ResourceFingerprint != origin.ResourceFingerprint ||
                prior.Scope != origin.Scope || prior.ExternalName != origin.ExternalName || prior.NativeId != origin.NativeId))
                return Result<ImportPreview>.Failure("This inventory name belongs to a different observed scope.", ErrorType.Conflict);
        }
        var link = instance is not null && item.Kind != "client" && !instance.Dependencies.Any(d =>
            d.TargetId == target && d.TargetKind == Kind(selection) && (item.Kind == "queue" || existing.Length == 1 && d.AccountId == existing[0]));
        var application = instance is null ? null : s.Applications.Single(a => a.Instances.Any(i => i.Id == instance.Id));
        var destination = Kind(selection) switch {
            TargetKind.Application => application?.Name, TargetKind.MessageBroker => s.MessageBrokers.FirstOrDefault(b => b.Id == target)?.Name,
            _ => s.DataStores.FirstOrDefault(d => d.Id == target)?.Name
        };
        if (destination is null) return Result<ImportPreview>.Failure("Inventory target no longer exists.", ErrorType.Conflict);
        return Result<ImportPreview>.Success(new(selection, source.UpdatedAt, existing.Cast<Guid?>().FirstOrDefault(), destination, link,
            target.Value, origin.ResourceFingerprint, needsOrigin, application?.Name, application?.Id, instance?.EnvironmentId));
    }

    private static TargetKind Kind(ImportSelection selection) => selection.Item.Kind == "client" ? TargetKind.Application :
        selection.Provider is "rabbitmq" or "servicebus" ? TargetKind.MessageBroker : TargetKind.DataStore;

    private static InventoryOrigin Origin(Snapshot s, ImportSelection selection)
    {
        var fingerprint = ReviewSources.Scope(s, new(selection.Provider, selection.IntegrationId, "", null, []));
        // RabbitMQ internal users live on the broker. A vhost's permission and topology evidence has narrower scope.
        if (selection is { Provider: "rabbitmq", Item.Kind: "account" })
        {
            var broker = s.RabbitMqIntegrations.Single(x => x.Id == selection.IntegrationId);
            fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { broker.Endpoint, broker.MessageBrokerId })));
        }
        return new(selection.Provider, selection.IntegrationId, fingerprint, selection.Item.Scope, selection.Item.Name, selection.Item.Coverage, selection.Item.NativeId);
    }

    private static Guid[] Existing(Snapshot s, ImportSelection selection, Guid target)
    {
        var item = selection.Item;
        if (item.Kind == "queue") return s.MessageBrokers.First(b => b.Id == target).Queues?.Where(q => q.Name == item.Name).Select(q => q.Id).ToArray() ?? [];
        return s.Accounts.Where(a => a.TargetId == target && a.TargetKind == Kind(selection) && (item.Kind == "client"
                ? a.AuthKind == AuthKind.OAuthClient && a.Parameters?.GetValueOrDefault("keycloakIssuer")?.TrimEnd('/') == item.Scope
                  && a.Parameters.GetValueOrDefault("keycloakClientId") == item.Name
                : a.AuthKind == AuthKind.UserPassword && a.UserName == item.Name && (selection.Provider != "mongodb" || a.Parameters?.GetValueOrDefault("authSource") == item.Scope)))
            .Select(a => a.Id).ToArray();
    }

    public async Task<Result<ImportReceipt>> ApplyAsync(ImportPreview preview, CancellationToken ct, Func<Snapshot, ImportPreview, bool>? allowed = null)
    {
        var selection = preview.Selection;
        var observed = await PreviewAsync(selection, ct);
        if (!observed.IsSuccess) return Result<ImportReceipt>.Failure(observed.Error!, observed.ErrorType!.Value);
        var result = Result<ImportReceipt>.Failure("Inventory or connection changed. Preview the import again.", ErrorType.Conflict);
        var recordedOrigin = false;
        await store.UpdateAsync(s =>
        {
            if (allowed is not null && !allowed(s, preview))
            {
                result = Result<ImportReceipt>.Failure("Permission to import this inventory changed.", ErrorType.Unauthorized);
                return s;
            }
            var source = Source(s, selection);
            if (source is null || source.UpdatedAt != preview.ConnectionVersion) return s;
            var current = Prepare(s, observed.Value!.Selection, source).Value;
            if (current is null || current.Selection.Item.Coverage != preview.Selection.Item.Coverage ||
                current.ResourceFingerprint != preview.ResourceFingerprint || current.Destination != preview.Destination ||
                current.DestinationId != preview.DestinationId || current.Application != preview.Application || current.ApplicationId != preview.ApplicationId ||
                current.EnvironmentId != preview.EnvironmentId ||
                current.RecordOrigin && !preview.RecordOrigin || current.LinkDependency && !preview.LinkDependency || preview.ExistingId.HasValue && current.ExistingId != preview.ExistingId) return s;
            var item = selection.Item;
            var now = DateTime.UtcNow;
            var id = current.ExistingId ?? Guid.NewGuid();
            var isClient = item.Kind == "client";
            var target = isClient ? selection.InstanceId!.Value : source.TargetId!.Value;
            var kind = Kind(selection);
            if (current.ExistingId is null)
            {
                if (item.Kind == "queue")
                    s = s with { MessageBrokers = s.MessageBrokers.Select(b => b.Id == target ? b with {
                        Queues = [.. b.Queues ?? [], new BrokerQueue(id, item.Name, null) { ImportedFrom = Origin(s, selection) }], UpdatedAt = now } : b).ToArray() };
                else
                {
                    Dictionary<string, string>? parameters = isClient ? new() { ["keycloakIssuer"] = item.Scope, ["keycloakClientId"] = item.Name }
                        : selection.Provider == "mongodb" ? new() { ["authSource"] = item.Scope } : null;
                    var account = new Models.Account(id, target, kind, isClient ? AuthKind.OAuthClient : AuthKind.UserPassword,
                        new(SecretBindingKind.None, null, null), item.Name, parameters, [], [], now, now) { ImportedFrom = Origin(s, selection) };
                    s = s with { Accounts = [.. s.Accounts, account] };
                }
            }
            if (current.RecordOrigin)
            {
                var origin = Origin(s, selection);
                if (item.Kind == "queue")
                    s = s with { MessageBrokers = s.MessageBrokers.Select(b => b.Id != target ? b : b with {
                        Queues = b.Queues!.Select(q => q.Id == id ? q with { ImportedFrom = origin } : q).ToArray(), UpdatedAt = now
                    }).ToArray() };
                else
                    s = s with { Accounts = s.Accounts.Select(a => a.Id == id ? a with { ImportedFrom = origin, UpdatedAt = now } : a).ToArray() };
                recordedOrigin = true;
            }
            if (current.LinkDependency)
            {
                var dep = new ApplicationInstanceDependency(Guid.NewGuid(), target, kind, null,
                    item.Kind == "queue" ? DependencyAuthKind.None : DependencyAuthKind.Account, item.Kind == "queue" ? null : id, null);
                s = s with { Applications = s.Applications.Select(a => a.Instances.Any(i => i.Id == selection.InstanceId)
                    ? a with { UpdatedAt = now, Instances = a.Instances.Select(i => i.Id == selection.InstanceId
                        ? i with { Dependencies = [.. i.Dependencies, dep], UpdatedAt = now } : i).ToArray() } : a).ToArray() };
            }
            result = Result<ImportReceipt>.Success(new(id, current.ExistingId is null, current.LinkDependency));
            return s;
        }, ct);
        if (result.Value is { } receipt && (receipt.Created || receipt.Linked || recordedOrigin))
            await audit.LogAsync(AuditHelper.CreateLog(AuditAction.IntegrationReviewImported, AuditArea.IntegrationReview,
                user.UserName, user.UserId, receipt.InventoryId, new { selection.Provider, selection.IntegrationId, selection.Item, selection.InstanceId }), ct);
        return result;
    }
}
