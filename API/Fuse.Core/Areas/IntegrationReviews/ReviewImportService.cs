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

public sealed record ObservedItem(string Kind, string Name, string Scope);
public sealed record ImportSelection(string Provider, Guid IntegrationId, ObservedItem Item, Guid? InstanceId = null);
public sealed record ImportPreview(ImportSelection Selection, DateTime ConnectionVersion, Guid? ExistingId, string Destination, bool LinkDependency);
public sealed record ImportReceipt(Guid InventoryId, bool Created, bool Linked);

public sealed class ReviewImportService(IFuseStore store, IAuditService audit, ICurrentUser user,
    IPostgreSqlIntegrationService postgres, IRedisIntegrationService redis, IMongoDbIntegrationService mongo,
    IRabbitMqIntegrationService rabbit, IKeycloakIntegrationService keycloak, IServiceBusIntegrationService bus)
{
    public async Task<IReadOnlyList<ObservedItem>> DiscoverAsync(string provider, Guid id, CancellationToken ct)
    {
        IEnumerable<ObservedItem>? items = provider switch
        {
            "postgresql" => (await postgres.DiscoverAsync(id, ct)).Value?.Roles.Where(r => r.CanLogin).Select(r => new ObservedItem("account", r.Name, "cluster")),
            "redis" => (await redis.DiscoverAsync(id, ct)).Value?.Users.Select(r => new ObservedItem("account", r.Username, "server")),
            "mongodb" => (await mongo.DiscoverAsync(id, ct)).Value?.Users.Select(r => new ObservedItem("account", r.Username, r.AuthenticationDatabase)),
            "servicebus" => (await bus.DiscoverAsync(id, ct)).Value?.Queues.Select(q => new ObservedItem("queue", q.Name, "namespace")),
            "rabbitmq" => RabbitItems((await rabbit.DiscoverAsync(id, ct)).Value),
            "keycloak" => ClientItems((await keycloak.DiscoverAsync(id, ct)).Value),
            _ => []
        };
        return (items ?? []).Distinct().OrderBy(i => i.Kind).ThenBy(i => i.Name, StringComparer.Ordinal).ThenBy(i => i.Scope).ToArray();
    }

    private static IEnumerable<ObservedItem> RabbitItems(Responses.RabbitMqDiscoveryResponse? d) =>
        d is null ? [] : d.Users.Select(u => new ObservedItem("account", u, d.VirtualHost))
            .Concat(d.Resources.Where(q => q.Kind == "queues").Select(q => new ObservedItem("queue", q.Name, d.VirtualHost)));
    private static IEnumerable<ObservedItem> ClientItems(Responses.KeycloakDiscoveryResponse? d) =>
        d is null ? [] : d.Clients.Select(r => new ObservedItem("client", r.ClientId, d.Issuer.AbsoluteUri.TrimEnd('/')));

    private static ReviewSource? Source(Snapshot s, ImportSelection selection) =>
        ReviewSources.All(s).FirstOrDefault(x => x.Provider == selection.Provider && x.Id == selection.IntegrationId);

    public async Task<Result<ImportPreview>> PreviewAsync(ImportSelection selection, CancellationToken ct)
    {
        if (selection.Item is null || string.IsNullOrWhiteSpace(selection.Item.Name))
            return Result<ImportPreview>.Failure("Select an observed item.");
        var source = Source(await store.GetAsync(ct), selection);
        if (source is null) return Result<ImportPreview>.Failure("Connection not found.", ErrorType.NotFound);
        var visible = await DiscoverAsync(selection.Provider, selection.IntegrationId, ct);
        if (!visible.Contains(selection.Item)) return Result<ImportPreview>.Failure("The selected item is no longer visible. Inspect again.", ErrorType.Conflict);
        var s = await store.GetAsync(ct);
        if (Source(s, selection) != source) return Result<ImportPreview>.Failure("The connection changed during discovery.", ErrorType.Conflict);
        return Prepare(s, selection, source);
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
        var link = instance is not null && item.Kind != "client" && !instance.Dependencies.Any(d =>
            d.TargetId == target && (item.Kind == "queue" || existing.Length == 1 && d.AccountId == existing[0]));
        var destination = item.Kind == "client" ? s.Applications.Single(a => a.Instances.Any(i => i.Id == target)).Name :
            s.DataStores.FirstOrDefault(d => d.Id == target)?.Name ?? s.MessageBrokers.FirstOrDefault(b => b.Id == target)?.Name;
        if (destination is null) return Result<ImportPreview>.Failure("Inventory target no longer exists.", ErrorType.Conflict);
        return Result<ImportPreview>.Success(new(selection, source.UpdatedAt, existing.Cast<Guid?>().FirstOrDefault(), destination, link));
    }

    private static Guid[] Existing(Snapshot s, ImportSelection selection, Guid target)
    {
        var item = selection.Item;
        if (item.Kind == "queue") return s.MessageBrokers.First(b => b.Id == target).Queues?.Where(q => q.Name == item.Name).Select(q => q.Id).ToArray() ?? [];
        return s.Accounts.Where(a => a.TargetId == target && (item.Kind == "client"
                ? a.AuthKind == AuthKind.OAuthClient && a.Parameters?.GetValueOrDefault("keycloakIssuer")?.TrimEnd('/') == item.Scope
                  && a.Parameters.GetValueOrDefault("keycloakClientId") == item.Name
                : a.UserName == item.Name && (selection.Provider != "mongodb" || a.Parameters?.GetValueOrDefault("authSource") == item.Scope)))
            .Select(a => a.Id).ToArray();
    }

    public async Task<Result<ImportReceipt>> ApplyAsync(ImportPreview preview, CancellationToken ct)
    {
        var selection = preview.Selection;
        var observed = await PreviewAsync(selection, ct);
        if (!observed.IsSuccess) return Result<ImportReceipt>.Failure(observed.Error!, observed.ErrorType!.Value);
        var result = Result<ImportReceipt>.Failure("Inventory or connection changed. Preview the import again.", ErrorType.Conflict);
        await store.UpdateAsync(s =>
        {
            var source = Source(s, selection);
            if (source is null || source.UpdatedAt != preview.ConnectionVersion) return s;
            var current = Prepare(s, selection, source).Value;
            if (current is null || current.LinkDependency && !preview.LinkDependency || preview.ExistingId.HasValue && current.ExistingId != preview.ExistingId) return s;
            var item = selection.Item;
            var now = DateTime.UtcNow;
            var id = current.ExistingId ?? Guid.NewGuid();
            var isClient = item.Kind == "client";
            var target = isClient ? selection.InstanceId!.Value : source.TargetId!.Value;
            var kind = isClient ? TargetKind.Application : selection.Provider is "rabbitmq" or "servicebus" ? TargetKind.MessageBroker : TargetKind.DataStore;
            if (current.ExistingId is null)
            {
                if (item.Kind == "queue")
                    s = s with { MessageBrokers = s.MessageBrokers.Select(b => b.Id == target ? b with {
                        Queues = [.. b.Queues ?? [], new BrokerQueue(id, item.Name, null)], UpdatedAt = now } : b).ToArray() };
                else
                {
                    Dictionary<string, string>? parameters = isClient ? new() { ["keycloakIssuer"] = item.Scope, ["keycloakClientId"] = item.Name }
                        : selection.Provider == "mongodb" ? new() { ["authSource"] = item.Scope } : null;
                    var account = new Models.Account(id, target, kind, isClient ? AuthKind.OAuthClient : AuthKind.UserPassword,
                        new(SecretBindingKind.None, null, null), item.Name, parameters, [], [], now, now);
                    s = s with { Accounts = [.. s.Accounts, account] };
                }
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
        if (result.Value is { } receipt && (receipt.Created || receipt.Linked))
            await audit.LogAsync(AuditHelper.CreateLog(AuditAction.IntegrationReviewImported, AuditArea.IntegrationReview,
                user.UserName, user.UserId, receipt.InventoryId, new { selection.Provider, selection.IntegrationId, selection.Item, selection.InstanceId }), ct);
        return result;
    }
}
