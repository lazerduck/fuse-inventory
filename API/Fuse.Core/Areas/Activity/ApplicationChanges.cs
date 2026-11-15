using Fuse.Core.Areas.DataStore;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.ExternalResource;
using Fuse.Core.Areas.MessageBroker;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.Tag;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Activity;

public record ApplicationChange(string Field, string? Before, string? After);
public record ApplicationChangeGroup(string Title, Guid? InstanceId, IReadOnlyList<ApplicationChange> Changes);
public record ApplicationChanges(string ApplicationName, string? Notice, IReadOnlyList<ApplicationChangeGroup> Groups);

public sealed class ApplicationChangeComparison(Snapshot inventory, Func<string, bool> canRead)
{
    public ApplicationChanges Compare(EntityVersion selected, string? previousSnapshot)
    {
        var after = Read(selected.EntitySnapshot);
        var before = Read(previousSnapshot);
        var name = after?.Name ?? before?.Name ?? $"Application {selected.EntityId}";
        if (before is null && selected.Action != AuditAction.ApplicationCreated)
            return new(name, "The previous application version is unavailable for this entry.", []);
        if (after is null && selected.Action != AuditAction.ApplicationDeleted)
            return new(name, "The application snapshot is unavailable for this entry.", []);

        var groups = new List<ApplicationChangeGroup>();
        var fields = new List<ApplicationChange>();
        Add(fields, "Name", before?.Name, after?.Name);
        Add(fields, "Version", before?.Version, after?.Version);
        Add(fields, "Description", before?.Description, after?.Description);
        Add(fields, "Owner", before?.Owner, after?.Owner);
        Add(fields, "Notes", before?.Notes, after?.Notes);
        Add(fields, "Framework", before?.Framework, after?.Framework);
        Add(fields, "Repository URL", before?.RepositoryUri, after?.RepositoryUri);
        Add(fields, "Icon", before?.Icon, after?.Icon);
        Add(fields, "Tags", Tags(before?.TagIds), Tags(after?.TagIds));
        foreach (var (old, current) in Match(before?.Pipelines, after?.Pipelines, p => p.Id))
        {
            var label = $"Pipeline · {(current ?? old)!.Name}";
            Add(fields, label, old?.Name, current?.Name);
            Add(fields, $"{label} URL", old?.PipelineUri, current?.PipelineUri);
        }
        if (fields.Count > 0)
            groups.Add(new("Application", null, fields));

        foreach (var (old, current) in Match(before?.Instances, after?.Instances, i => i.Id))
        {
            var changes = InstanceChanges(old, current);
            if (changes.Count == 0) continue;
            var instance = (current ?? old)!;
            var status = old is null ? "Instance added" : current is null ? "Instance removed" : "Instance";
            groups.Add(new($"{status} · {EnvironmentName(instance.EnvironmentId)}", instance.Id, changes));
        }
        return new(name, null, groups);
    }

    private List<ApplicationChange> InstanceChanges(ApplicationInstance? old, ApplicationInstance? current)
    {
        var fields = new List<ApplicationChange>();
        if (old?.EnvironmentId != current?.EnvironmentId)
            fields.Add(new("Environment", EnvironmentName(old?.EnvironmentId), EnvironmentName(current?.EnvironmentId)));
        if (old?.PlatformId != current?.PlatformId)
            fields.Add(new("Platform", PlatformName(old?.PlatformId), PlatformName(current?.PlatformId)));
        Add(fields, "Deployed version", old?.Version, current?.Version);
        Add(fields, "Base URL", old?.BaseUri, current?.BaseUri);
        Add(fields, "Health URL", old?.HealthUri, current?.HealthUri);
        Add(fields, "OpenAPI URL", old?.OpenApiUri, current?.OpenApiUri);
        Add(fields, "App Configuration provider", old?.AppConfigurationProviderId, current?.AppConfigurationProviderId);
        Add(fields, "App Configuration key suffix", old?.AppConfigurationKeySuffix, current?.AppConfigurationKeySuffix);
        Add(fields, "Tags", Tags(old?.TagIds), Tags(current?.TagIds));
        var previousKey = old?.ApiKey is { Kind: not SecretBindingKind.None } ? old.ApiKey : null;
        var currentKey = current?.ApiKey is { Kind: not SecretBindingKind.None } ? current.ApiKey : null;
        if (previousKey != currentKey)
            fields.Add(new("API key binding", previousKey?.Kind.ToString(), currentKey is null ? null :
                previousKey?.Kind == currentKey.Kind ? $"{currentKey.Kind} (binding changed)" : currentKey.Kind.ToString()));

        foreach (var (removed, added) in Match(old?.Dependencies, current?.Dependencies, d => d.Id))
        {
            if (removed == added) continue;
            var action = removed is null ? "Dependency added" : added is null ? "Dependency removed" : "Dependency changed";
            var previous = Dependency(removed);
            var next = Dependency(added);
            if (previous == next && removed?.TargetId != added?.TargetId)
            {
                previous += $" ({removed!.TargetId})";
                next += $" ({added!.TargetId})";
            }
            fields.Add(new(action, previous, next));
        }
        return fields;
    }

    private string? Dependency(ApplicationInstanceDependency? dependency)
    {
        if (dependency is null) return null;
        var id = dependency.TargetId;
        var name = dependency.TargetKind switch
        {
            TargetKind.Application => ApplicationTarget(id),
            TargetKind.DataStore when canRead(DataStorePermissions.ReadKey) => inventory.DataStores.FirstOrDefault(d => d.Id == id)?.Name,
            TargetKind.External when canRead(ExternalResourcePermissions.ReadKey) => inventory.ExternalResources.FirstOrDefault(r => r.Id == id)?.Name,
            TargetKind.MessageBroker when canRead(MessageBrokerPermissions.ReadKey) => inventory.MessageBrokers.FirstOrDefault(b => b.Id == id)?.Name,
            _ => null
        };
        var kind = dependency.TargetKind switch
        {
            TargetKind.DataStore => "Data store",
            TargetKind.External => "External resource",
            TargetKind.MessageBroker => "Message broker",
            _ => "Application"
        };
        var details = new List<string>();
        if (dependency.Port is not null) details.Add($"Port {dependency.Port}");
        details.Add($"{dependency.Severity} dependency");
        details.Add(dependency.AuthKind == DependencyAuthKind.None ? "No authentication" : $"{dependency.AuthKind} authentication");
        if (dependency.AccountId is not null) details.Add($"Account {dependency.AccountId}");
        if (dependency.IdentityId is not null) details.Add($"Identity {dependency.IdentityId}");
        return $"{name ?? id.ToString()} ({kind})\n{string.Join(" · ", details)}";
    }

    private string? ApplicationTarget(Guid id)
    {
        foreach (var app in inventory.Applications)
        {
            if (app.Id == id) return app.Name;
            var instance = app.Instances?.FirstOrDefault(i => i.Id == id);
            if (instance is not null) return $"{app.Name} · {EnvironmentName(instance.EnvironmentId)}";
        }
        return null;
    }

    private string? EnvironmentName(Guid? id) =>
        (canRead(EnvironmentPermissions.ReadKey) ? inventory.Environments.FirstOrDefault(e => e.Id == id)?.Name : null) ?? id?.ToString();

    private string? PlatformName(Guid? id) =>
        (canRead(PlatformPermissions.ReadKey) ? inventory.Platforms.FirstOrDefault(p => p.Id == id)?.DisplayName : null) ?? id?.ToString();

    private string? Tags(HashSet<Guid>? ids) => ids is { Count: > 0 }
        ? string.Join("\n", ids.Order().Select(id => canRead(TagPermissions.ReadKey)
            ? inventory.Tags.FirstOrDefault(t => t.Id == id)?.Name ?? id.ToString() : id.ToString()))
        : null;

    private static Models.Application? Read(string? json) =>
        (Models.Application?)EntityExtractor.DeserializeEntity(json, EntityType.Application);

    private static void Add<T>(List<ApplicationChange> fields, string label, T before, T after)
    {
        var oldText = before?.ToString();
        var newText = after?.ToString();
        if (oldText != newText) fields.Add(new(label, oldText, newText));
    }

    private static IEnumerable<(T? Before, T? After)> Match<T>(IEnumerable<T>? before, IEnumerable<T>? after,
        Func<T, Guid> key) where T : class
    {
        var previous = (before ?? []).ToDictionary(key);
        var current = (after ?? []).ToDictionary(key);
        foreach (var id in previous.Keys.Union(current.Keys))
            yield return (previous.GetValueOrDefault(id), current.GetValueOrDefault(id));
    }
}
