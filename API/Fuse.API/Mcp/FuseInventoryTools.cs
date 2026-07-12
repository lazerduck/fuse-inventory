using System.ComponentModel;
using System.Text.Json;
using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.Tag;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using ModelContextProtocol.Server;
using ApplicationModel = Fuse.Core.Models.Application;

namespace Fuse.API.Mcp;

[McpServerToolType]
public sealed class FuseInventoryTools(
    IApplicationService applications,
    IApplicationHealthService health,
    IEnvironmentService environments,
    IPlatformService platforms,
    ITagService tags,
    McpToolAuthorization authorization)
{
    [McpServerTool(Name = "inventory_list_applications", ReadOnly = true)]
    [Description("List safe application and instance summaries in the Fuse inventory.")]
    public async Task<object> ListApplications(
        string? name = null, Guid? environmentId = null, Guid? tagId = null,
        bool incompleteOnly = false, CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync(ApplicationPermissions.ReadKey, cancellationToken);
        var all = await applications.GetApplicationsAsync();
        var healthById = incompleteOnly
            ? (await health.GetAllApplicationHealths()).ToDictionary(x => x.ApplicationId)
            : [];

        return all
            .Where(a => string.IsNullOrWhiteSpace(name)
                || a.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Where(a => environmentId is null || a.Instances.Any(i => i.EnvironmentId == environmentId))
            .Where(a => tagId is null || a.TagIds.Contains(tagId.Value) || a.Instances.Any(i => i.TagIds.Contains(tagId.Value)))
            .Where(a => !incompleteOnly || healthById.TryGetValue(a.Id, out var h) && HasGaps(h))
            .Select(a => new
            {
                a.Id, a.Name, a.Version, a.Description, a.Owner, a.Framework, a.RepositoryUri,
                a.TagIds, a.UpdatedAt,
                Instances = a.Instances.Select(SafeInstance)
            });
    }

    [McpServerTool(Name = "inventory_get_application", ReadOnly = true)]
    [Description("Get one application and its instances without secret values.")]
    public async Task<object> GetApplication(Guid applicationId, CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync(ApplicationPermissions.ReadKey, cancellationToken);
        var app = await GetRequiredApplication(applicationId);
        return SafeApplication(app);
    }

    [McpServerTool(Name = "inventory_review_completeness", ReadOnly = true)]
    [Description("Review missing application and instance documentation fields.")]
    public async Task<object> ReviewCompleteness(Guid? applicationId = null, CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync(ApplicationPermissions.ReadKey, cancellationToken);
        var entries = applicationId is null
            ? await health.GetAllApplicationHealths()
            : [await health.GetApplicationHealth(applicationId.Value)];
        return entries.Select(ToCompletenessReview);
    }

    [McpServerTool(Name = "inventory_list_reference_data", ReadOnly = true)]
    [Description("List environments, platforms, and tags that may be referenced by inventory updates.")]
    public async Task<object> ListReferenceData(CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync(ApplicationPermissions.ReadKey, cancellationToken);
        await authorization.RequireAsync(EnvironmentPermissions.ReadKey, cancellationToken);
        await authorization.RequireAsync(PlatformPermissions.ReadKey, cancellationToken);
        await authorization.RequireAsync(TagPermissions.ReadKey, cancellationToken);
        return new
        {
            Environments = await environments.GetEnvironments(),
            Platforms = (await platforms.GetPlatformsAsync()).Select(p => new { p.Id, p.DisplayName, p.DnsName, p.Kind, p.TagIds }),
            Tags = await tags.GetTagsAsync()
        };
    }

    [McpServerTool(Name = "inventory_update_application_documentation", Destructive = false)]
    [Description("Patch documented application fields. The expected timestamp prevents overwriting concurrent changes.")]
    public async Task<object> UpdateApplicationDocumentation(
        Guid applicationId, DateTime expectedUpdatedAt,
        [Description("JSON object containing only version, description, owner, notes, framework, repositoryUri, icon, or tagIds. JSON null clears nullable fields.")]
        JsonElement changes,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync(ApplicationPermissions.UpdateKey, cancellationToken);
        var app = await GetRequiredApplication(applicationId);
        EnsureCurrent(app.UpdatedAt, expectedUpdatedAt);
        EnsureObject(changes);
        EnsureOnly(changes, "version", "description", "owner", "notes", "framework", "repositoryUri", "icon", "tagIds");

        var command = new UpdateApplication(
            app.Id, app.Name,
            PatchString(changes, "version", app.Version),
            PatchString(changes, "description", app.Description),
            PatchString(changes, "owner", app.Owner),
            PatchString(changes, "notes", app.Notes),
            PatchString(changes, "framework", app.Framework),
            PatchUri(changes, "repositoryUri", app.RepositoryUri),
            PatchString(changes, "icon", app.Icon),
            PatchGuids(changes, "tagIds", app.TagIds));
        var result = await applications.UpdateApplicationAsync(command);
        if (!result.IsSuccess)
            throw new InvalidOperationException(result.Error);
        return new { Before = SafeApplication(app), After = SafeApplication(result.Value!), Completeness = ToCompletenessReview(await health.GetApplicationHealth(app.Id)) };
    }

    [McpServerTool(Name = "inventory_update_instance_documentation", Destructive = false)]
    [Description("Patch documented instance fields. The expected timestamp prevents overwriting concurrent changes.")]
    public async Task<object> UpdateInstanceDocumentation(
        Guid applicationId, Guid instanceId, DateTime expectedUpdatedAt,
        [Description("JSON object containing only platformId, baseUri, healthUri, openApiUri, version, or tagIds. JSON null clears nullable fields.")]
        JsonElement changes,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync(ApplicationPermissions.UpdateInstanceKey, cancellationToken);
        var app = await GetRequiredApplication(applicationId);
        var instance = app.Instances.FirstOrDefault(i => i.Id == instanceId)
            ?? throw new KeyNotFoundException($"Application instance '{instanceId}' was not found.");
        EnsureCurrent(instance.UpdatedAt, expectedUpdatedAt);
        EnsureObject(changes);
        EnsureOnly(changes, "platformId", "baseUri", "healthUri", "openApiUri", "version", "tagIds");

        var command = new UpdateApplicationInstance(
            app.Id, instance.Id, instance.EnvironmentId,
            PatchGuid(changes, "platformId", instance.PlatformId),
            PatchUri(changes, "baseUri", instance.BaseUri),
            PatchUri(changes, "healthUri", instance.HealthUri),
            PatchUri(changes, "openApiUri", instance.OpenApiUri),
            PatchString(changes, "version", instance.Version),
            PatchGuids(changes, "tagIds", instance.TagIds),
            instance.ApiKey, instance.AppConfigurationProviderId, instance.AppConfigurationKeySuffix);
        var result = await applications.UpdateInstanceAsync(command);
        if (!result.IsSuccess)
            throw new InvalidOperationException(result.Error);
        return new { Before = SafeInstance(instance), After = SafeInstance(result.Value!), Completeness = ToCompletenessReview(await health.GetApplicationHealth(app.Id)) };
    }

    private async Task<ApplicationModel> GetRequiredApplication(Guid id) =>
        await applications.GetApplicationByIdAsync(id)
        ?? throw new KeyNotFoundException($"Application '{id}' was not found.");

    private static object SafeApplication(ApplicationModel a) => new
    {
        a.Id, a.Name, a.Version, a.Description, a.Owner, a.Notes, a.Framework, a.RepositoryUri,
        a.Icon, a.TagIds, a.Pipelines, a.CreatedAt, a.UpdatedAt,
        Instances = a.Instances.Select(SafeInstance)
    };

    private static object SafeInstance(ApplicationInstance i) => new
    {
        i.Id, i.EnvironmentId, i.PlatformId, i.BaseUri, i.HealthUri, i.OpenApiUri, i.Version,
        i.Dependencies, i.TagIds, i.AppConfigurationProviderId, i.AppConfigurationKeySuffix,
        HasApiKey = i.ApiKey is not null && i.ApiKey.Kind != SecretBindingKind.None,
        i.CreatedAt, i.UpdatedAt
    };

    private static object ToCompletenessReview(ApplicationHealth h)
    {
        var gaps = new List<object>();
        if (!h.VersionSet) gaps.Add(new { Scope = "application", Field = "version", Severity = "required" });
        if (!h.DescriptionSet) gaps.Add(new { Scope = "application", Field = "description", Severity = "required" });
        if (!h.OwnerSet) gaps.Add(new { Scope = "application", Field = "owner", Severity = "required" });
        if (!h.FrameworkSet) gaps.Add(new { Scope = "application", Field = "framework", Severity = "required" });
        foreach (var i in h.InstanceHealths)
        {
            if (!i.PlatformSet) gaps.Add(new { Scope = "instance", Field = "platformId", Severity = "required", i.InstanceId });
            if (!i.BaseUriSet) gaps.Add(new { Scope = "instance", Field = "baseUri", Severity = "recommended", i.InstanceId });
            if (!i.HealthUriSet) gaps.Add(new { Scope = "instance", Field = "healthUri", Severity = "recommended", i.InstanceId });
            if (!i.OpenApiUriSet) gaps.Add(new { Scope = "instance", Field = "openApiUri", Severity = "recommended", i.InstanceId });
            if (!i.VersionSet) gaps.Add(new { Scope = "instance", Field = "version", Severity = "required", i.InstanceId });
        }
        return new { h.ApplicationId, IsComplete = gaps.Count == 0, Gaps = gaps };
    }

    private static bool HasGaps(ApplicationHealth h) =>
        !h.VersionSet || !h.DescriptionSet || !h.OwnerSet || !h.FrameworkSet
        || h.InstanceHealths.Any(i => !i.PlatformSet || !i.BaseUriSet || !i.HealthUriSet || !i.OpenApiUriSet || !i.VersionSet);

    private static void EnsureCurrent(DateTime actual, DateTime expected)
    {
        if (actual.ToUniversalTime() != expected.ToUniversalTime())
            throw new InvalidOperationException($"The record changed after it was read. Current updatedAt is {actual:O}.");
    }

    private static void EnsureObject(JsonElement changes)
    {
        if (changes.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("changes must be a JSON object.");
    }

    private static void EnsureOnly(JsonElement changes, params string[] allowed)
    {
        var set = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = changes.EnumerateObject().FirstOrDefault(p => !set.Contains(p.Name));
        if (invalid.Name is not null)
            throw new ArgumentException($"Field '{invalid.Name}' cannot be changed by this tool.");
    }

    private static string? PatchString(JsonElement o, string name, string? current) =>
        !o.TryGetProperty(name, out var value) ? current
        : value.ValueKind == JsonValueKind.Null ? null
        : value.ValueKind == JsonValueKind.String ? value.GetString()
        : throw new ArgumentException($"{name} must be a string or null.");

    private static Uri? PatchUri(JsonElement o, string name, Uri? current)
    {
        var value = PatchString(o, name, current?.ToString());
        if (value is null) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new ArgumentException($"{name} must be an absolute URI or null.");
        return uri;
    }

    private static Guid? PatchGuid(JsonElement o, string name, Guid? current)
    {
        if (!o.TryGetProperty(name, out var value)) return current;
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id)) return id;
        throw new ArgumentException($"{name} must be a UUID string or null.");
    }

    private static HashSet<Guid> PatchGuids(JsonElement o, string name, HashSet<Guid> current)
    {
        if (!o.TryGetProperty(name, out var value)) return current;
        if (value.ValueKind != JsonValueKind.Array)
            throw new ArgumentException($"{name} must be an array of UUID strings.");
        try { return value.EnumerateArray().Select(x => x.GetGuid()).ToHashSet(); }
        catch (Exception e) when (e is FormatException or InvalidOperationException)
        { throw new ArgumentException($"{name} must contain only UUID strings."); }
    }
}
