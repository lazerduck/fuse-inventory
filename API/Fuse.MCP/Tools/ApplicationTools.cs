using System.ComponentModel;
using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.Tag;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ApplicationModel = Fuse.Core.Models.Application;

namespace Fuse.MCP;

[McpServerToolType]
public sealed class ApplicationTools(
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

    [McpServerTool(Name = "inventory_patch_application", Destructive = true)]
    [Description("Patch documented application fields. The expected timestamp prevents overwriting concurrent changes.")]
    public async Task<object> UpdateApplicationDocumentation(
        Guid applicationId, DateTime expectedUpdatedAt,
        ApplicationDocumentationPatch changes,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync(ApplicationPermissions.UpdateKey, cancellationToken);
        var app = await GetRequiredApplication(applicationId);
        EnsureCurrent(app.UpdatedAt, expectedUpdatedAt);
        ValidateClearFields(changes.ClearFields, ApplicationDocumentationPatch.ClearableFields);

        var command = new UpdateApplication(
            app.Id, app.Name,
            PatchValue(changes.Version, app.Version, changes.Clears("version"), "version"),
            PatchValue(changes.Description, app.Description, changes.Clears("description"), "description"),
            PatchValue(changes.Owner, app.Owner, changes.Clears("owner"), "owner"),
            PatchValue(changes.Notes, app.Notes, changes.Clears("notes"), "notes"),
            PatchValue(changes.Framework, app.Framework, changes.Clears("framework"), "framework"),
            PatchUri(changes.RepositoryUri, app.RepositoryUri, changes.Clears("repositoryUri"), "repositoryUri"),
            PatchValue(changes.Icon, app.Icon, changes.Clears("icon"), "icon"),
            changes.TagIds?.ToHashSet() ?? app.TagIds);
        var result = await applications.UpdateApplicationAsync(command);
        if (!result.IsSuccess)
            throw new McpException(result.Error ?? "The application update failed.");
        return new { Before = SafeApplication(app), After = SafeApplication(result.Value!), Completeness = ToCompletenessReview(await health.GetApplicationHealth(app.Id)) };
    }

    [McpServerTool(Name = "inventory_patch_application_instance", Destructive = true)]
    [Description("Patch documented instance fields. The expected timestamp prevents overwriting concurrent changes.")]
    public async Task<object> UpdateInstanceDocumentation(
        Guid applicationId, Guid instanceId, DateTime expectedUpdatedAt,
        InstanceDocumentationPatch changes,
        CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync(ApplicationPermissions.UpdateInstanceKey, cancellationToken);
        var app = await GetRequiredApplication(applicationId);
        var instance = app.Instances.FirstOrDefault(i => i.Id == instanceId)
            ?? throw new McpException($"Application instance '{instanceId}' was not found.");
        EnsureCurrent(instance.UpdatedAt, expectedUpdatedAt);
        ValidateClearFields(changes.ClearFields, InstanceDocumentationPatch.ClearableFields);

        var command = new UpdateApplicationInstance(
            app.Id, instance.Id, instance.EnvironmentId,
            PatchValue(changes.PlatformId, instance.PlatformId, changes.Clears("platformId"), "platformId"),
            PatchUri(changes.BaseUri, instance.BaseUri, changes.Clears("baseUri"), "baseUri"),
            PatchUri(changes.HealthUri, instance.HealthUri, changes.Clears("healthUri"), "healthUri"),
            PatchUri(changes.OpenApiUri, instance.OpenApiUri, changes.Clears("openApiUri"), "openApiUri"),
            PatchValue(changes.Version, instance.Version, changes.Clears("version"), "version"),
            changes.TagIds?.ToHashSet() ?? instance.TagIds,
            instance.ApiKey, instance.AppConfigurationProviderId, instance.AppConfigurationKeySuffix);
        var result = await applications.UpdateInstanceAsync(command);
        if (!result.IsSuccess)
            throw new McpException(result.Error ?? "The application instance update failed.");
        return new { Before = SafeInstance(instance), After = SafeInstance(result.Value!), Completeness = ToCompletenessReview(await health.GetApplicationHealth(app.Id)) };
    }

    [McpServerTool(Name = "inventory_create_application", Destructive = false)]
    public async Task<object> CreateApplication(CreateApplication command, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.CreateKey, ct); return McpResult.Value(await applications.CreateApplicationAsync(command)); }

    [McpServerTool(Name = "inventory_replace_application", Destructive = true)]
    [Description("Replace an application's complete definition, including its name. Read it first to preserve unchanged fields.")]
    public async Task<object> ReplaceApplication(UpdateApplication command, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.UpdateKey, ct); return McpResult.Value(await applications.UpdateApplicationAsync(command)); }

    [McpServerTool(Name = "inventory_delete_application", Destructive = true)]
    public async Task<object> DeleteApplication(Guid applicationId, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.DeleteKey, ct); return McpResult.Done(await applications.DeleteApplicationAsync(new(applicationId))); }

    [McpServerTool(Name = "inventory_create_application_instance", Destructive = false)]
    public async Task<object> CreateInstance(CreateApplicationInstance command, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.CreateInstanceKey, ct); return McpResult.Value(await applications.CreateInstanceAsync(command)); }

    [McpServerTool(Name = "inventory_replace_application_instance", Destructive = true)]
    public async Task<object> ReplaceInstance(UpdateApplicationInstance command, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.UpdateInstanceKey, ct); return McpResult.Value(await applications.UpdateInstanceAsync(command)); }

    [McpServerTool(Name = "inventory_delete_application_instance", Destructive = true)]
    public async Task<object> DeleteInstance(Guid applicationId, Guid instanceId, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.DeleteInstanceKey, ct); return McpResult.Done(await applications.DeleteInstanceAsync(new(applicationId, instanceId))); }

    [McpServerTool(Name = "inventory_create_application_pipeline", Destructive = false)]
    public async Task<object> CreatePipeline(CreateApplicationPipeline command, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.CreateInstanceKey, ct); return McpResult.Value(await applications.CreatePipelineAsync(command)); }

    [McpServerTool(Name = "inventory_replace_application_pipeline", Destructive = true)]
    public async Task<object> ReplacePipeline(UpdateApplicationPipeline command, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.UpdateInstanceKey, ct); return McpResult.Value(await applications.UpdatePipelineAsync(command)); }

    [McpServerTool(Name = "inventory_delete_application_pipeline", Destructive = true)]
    public async Task<object> DeletePipeline(Guid applicationId, Guid pipelineId, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.DeleteInstanceKey, ct); return McpResult.Done(await applications.DeletePipelineAsync(new(applicationId, pipelineId))); }

    [McpServerTool(Name = "inventory_create_application_dependency", Destructive = false)]
    public async Task<object> CreateDependency(ApplicationDependencyInput command, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.UpdateInstanceKey, ct); return McpResult.Value(await applications.CreateDependencyAsync(command.ToCreate())); }

    [McpServerTool(Name = "inventory_replace_application_dependency", Destructive = true)]
    public async Task<object> ReplaceDependency(ReplaceApplicationDependencyInput command, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.UpdateInstanceKey, ct); return McpResult.Value(await applications.UpdateDependencyAsync(command.ToUpdate())); }

    [McpServerTool(Name = "inventory_delete_application_dependency", Destructive = true)]
    public async Task<object> DeleteDependency(Guid applicationId, Guid instanceId, Guid dependencyId, CancellationToken ct = default)
    { await authorization.RequireAsync(ApplicationPermissions.UpdateInstanceKey, ct); return McpResult.Done(await applications.DeleteDependencyAsync(new(applicationId, instanceId, dependencyId))); }

    private async Task<ApplicationModel> GetRequiredApplication(Guid id) =>
        await applications.GetApplicationByIdAsync(id)
        ?? throw new McpException($"Application '{id}' was not found.");

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
            throw new McpException($"The record changed after it was read. Read it again and retry with updatedAt '{actual:O}'.");
    }

    private static void ValidateClearFields(IReadOnlyList<string>? fields, IReadOnlySet<string> allowed)
    {
        if (fields is null) return;
        var invalid = fields.FirstOrDefault(field => !allowed.Contains(field));
        if (invalid is not null)
            throw new McpException($"Field '{invalid}' cannot be cleared by this tool.");
    }

    private static T? PatchValue<T>(T? supplied, T? current, bool clear, string fieldName) where T : struct
    {
        if (clear && supplied is not null)
            throw new McpException($"'{fieldName}' cannot be supplied and cleared in the same update.");
        return clear ? null : supplied ?? current;
    }

    private static string? PatchValue(string? supplied, string? current, bool clear, string fieldName)
    {
        if (clear && supplied is not null)
            throw new McpException($"'{fieldName}' cannot be supplied and cleared in the same update.");
        return clear ? null : supplied ?? current;
    }

    private static Uri? PatchUri(string? supplied, Uri? current, bool clear, string fieldName)
    {
        if (clear && supplied is not null)
            throw new McpException($"'{fieldName}' cannot be supplied and cleared in the same update.");
        if (clear) return null;
        if (supplied is null) return current;
        if (!Uri.TryCreate(supplied, UriKind.Absolute, out var uri))
            throw new McpException($"'{fieldName}' must be an absolute URI.");
        return uri;
    }
}
