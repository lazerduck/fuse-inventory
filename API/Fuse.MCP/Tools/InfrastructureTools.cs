using Fuse.Core.Areas.DataStore;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.ExternalResource;
using Fuse.Core.Areas.MessageBroker;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.Tag;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Fuse.MCP;

[McpServerToolType]
public sealed class InfrastructureTools(
    IDataStoreService dataStores, IEnvironmentService environments,
    IExternalResourceService externalResources, IMessageBrokerService messageBrokers,
    IPlatformService platforms, ITagService tags, McpToolAuthorization authorization)
{
    [McpServerTool(Name = "inventory_create_datastore", Destructive = false)]
    public async Task<object> CreateDataStore(string name, string kind, Guid environmentId, Guid? platformId = null, string? connectionUri = null, IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default) { await Require(DataStorePermissions.CreateKey, ct); return McpResult.Value(await dataStores.CreateDataStoreAsync(new(name, kind, environmentId, platformId, Uri(connectionUri, "connectionUri"), tagIds?.ToHashSet()))); }
    [McpServerTool(Name = "inventory_replace_datastore", Destructive = true)]
    public async Task<object> ReplaceDataStore(Guid dataStoreId, string name, string kind, Guid environmentId, Guid? platformId = null, string? connectionUri = null, IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default) { await Require(DataStorePermissions.UpdateKey, ct); return McpResult.Value(await dataStores.UpdateDataStoreAsync(new(dataStoreId, name, kind, environmentId, platformId, Uri(connectionUri, "connectionUri"), tagIds?.ToHashSet()))); }
    [McpServerTool(Name = "inventory_delete_datastore", Destructive = true)]
    public async Task<object> DeleteDataStore(Guid dataStoreId, CancellationToken ct = default) { await Require(DataStorePermissions.DeleteKey, ct); return McpResult.Done(await dataStores.DeleteDataStoreAsync(new(dataStoreId))); }

    [McpServerTool(Name = "inventory_create_environment", Destructive = false)]
    public async Task<object> CreateEnvironment(string name, string? description = null, IReadOnlyList<Guid>? tagIds = null, bool autoCreateInstances = false, string? baseUriTemplate = null, string? healthUriTemplate = null, string? openApiUriTemplate = null, CancellationToken ct = default) { await Require(EnvironmentPermissions.CreateKey, ct); return McpResult.Value(await environments.CreateEnvironment(new(name, description, tagIds?.ToHashSet(), autoCreateInstances, baseUriTemplate, healthUriTemplate, openApiUriTemplate))); }
    [McpServerTool(Name = "inventory_replace_environment", Destructive = true)]
    public async Task<object> ReplaceEnvironment(Guid environmentId, string name, string? description = null, IReadOnlyList<Guid>? tagIds = null, bool autoCreateInstances = false, string? baseUriTemplate = null, string? healthUriTemplate = null, string? openApiUriTemplate = null, CancellationToken ct = default) { await Require(EnvironmentPermissions.UpdateKey, ct); return McpResult.Value(await environments.UpdateEnvironment(new(environmentId, name, description, tagIds?.ToHashSet(), autoCreateInstances, baseUriTemplate, healthUriTemplate, openApiUriTemplate))); }
    [McpServerTool(Name = "inventory_delete_environment", Destructive = true)]
    public async Task<object> DeleteEnvironment(Guid environmentId, CancellationToken ct = default) { await Require(EnvironmentPermissions.DeleteKey, ct); return McpResult.Done(await environments.DeleteEnvironmentAsync(new(environmentId))); }
    [McpServerTool(Name = "inventory_apply_environment_automation", Destructive = true)]
    public async Task<object> ApplyAutomation(Guid? environmentId = null, Guid? applicationId = null, CancellationToken ct = default) { await Require(EnvironmentPermissions.ApplyAutomationKey, ct); return McpResult.Value(await environments.ApplyEnvironmentAutomationAsync(new(environmentId, applicationId))); }

    [McpServerTool(Name = "inventory_create_external_resource", Destructive = false)]
    public async Task<object> CreateExternalResource(string name, string? description = null, string? resourceUri = null, IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default) { await Require(ExternalResourcePermissions.CreateKey, ct); return McpResult.Value(await externalResources.CreateExternalResourceAsync(new(name, description, Uri(resourceUri, "resourceUri"), tagIds?.ToHashSet()))); }
    [McpServerTool(Name = "inventory_replace_external_resource", Destructive = true)]
    public async Task<object> ReplaceExternalResource(Guid externalResourceId, string name, string? description = null, string? resourceUri = null, IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default) { await Require(ExternalResourcePermissions.UpdateKey, ct); return McpResult.Value(await externalResources.UpdateExternalResourceAsync(new(externalResourceId, name, description, Uri(resourceUri, "resourceUri"), tagIds?.ToHashSet()))); }
    [McpServerTool(Name = "inventory_delete_external_resource", Destructive = true)]
    public async Task<object> DeleteExternalResource(Guid externalResourceId, CancellationToken ct = default) { await Require(ExternalResourcePermissions.DeleteKey, ct); return McpResult.Done(await externalResources.DeleteExternalResourceAsync(new(externalResourceId))); }

    [McpServerTool(Name = "inventory_create_message_broker", Destructive = false)]
    public async Task<object> CreateMessageBroker(string name, string kind, Guid environmentId, string? description = null, string? connectionUri = null, IReadOnlyList<BrokerQueueInput>? queues = null, IReadOnlyList<BrokerTopicInput>? topics = null, IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default) { await Require(MessageBrokerPermissions.CreateKey, ct); return McpResult.Value(await messageBrokers.CreateMessageBrokerAsync(new(name, description, kind, environmentId, Uri(connectionUri, "connectionUri"), queues, topics, tagIds?.ToHashSet()))); }
    [McpServerTool(Name = "inventory_replace_message_broker", Destructive = true)]
    public async Task<object> ReplaceMessageBroker(Guid messageBrokerId, string name, string kind, Guid environmentId, string? description = null, string? connectionUri = null, IReadOnlyList<BrokerQueueInput>? queues = null, IReadOnlyList<BrokerTopicInput>? topics = null, IReadOnlyList<Guid>? tagIds = null, CancellationToken ct = default) { await Require(MessageBrokerPermissions.UpdateKey, ct); return McpResult.Value(await messageBrokers.UpdateMessageBrokerAsync(new(messageBrokerId, name, description, kind, environmentId, Uri(connectionUri, "connectionUri"), queues, topics, tagIds?.ToHashSet()))); }
    [McpServerTool(Name = "inventory_delete_message_broker", Destructive = true)]
    public async Task<object> DeleteMessageBroker(Guid messageBrokerId, CancellationToken ct = default) { await Require(MessageBrokerPermissions.DeleteKey, ct); return McpResult.Done(await messageBrokers.DeleteMessageBrokerAsync(new(messageBrokerId))); }

    [McpServerTool(Name = "inventory_create_platform", Destructive = false)]
    public async Task<object> CreatePlatform(string displayName, string? dnsName = null, string? os = null, PlatformKind? kind = null, IReadOnlyList<string>? ipAddresses = null, string? notes = null, IReadOnlyList<Guid>? tagIds = null, IReadOnlyList<PlatformNodeInput>? nodes = null, CancellationToken ct = default) { await Require(PlatformPermissions.CreateKey, ct); return McpResult.Value(await platforms.CreatePlatformAsync(new(displayName, dnsName, os, kind, ipAddresses, notes, tagIds?.ToHashSet(), nodes))); }
    [McpServerTool(Name = "inventory_replace_platform", Destructive = true)]
    public async Task<object> ReplacePlatform(Guid platformId, string displayName, string? dnsName = null, string? os = null, PlatformKind? kind = null, IReadOnlyList<string>? ipAddresses = null, string? notes = null, IReadOnlyList<Guid>? tagIds = null, IReadOnlyList<PlatformNodeInput>? nodes = null, CancellationToken ct = default) { await Require(PlatformPermissions.UpdateKey, ct); return McpResult.Value(await platforms.UpdatePlatformAsync(new(platformId, displayName, dnsName, os, kind, ipAddresses, notes, tagIds?.ToHashSet(), nodes))); }
    [McpServerTool(Name = "inventory_delete_platform", Destructive = true)]
    public async Task<object> DeletePlatform(Guid platformId, CancellationToken ct = default) { await Require(PlatformPermissions.DeleteKey, ct); return McpResult.Done(await platforms.DeletePlatformAsync(new(platformId))); }

    [McpServerTool(Name = "inventory_create_tag", Destructive = false)]
    public async Task<object> CreateTag(string name, string? description = null, TagColor? color = null, CancellationToken ct = default) { await Require(TagPermissions.CreateKey, ct); return McpResult.Value(await tags.CreateTagAsync(new(name, description, color))); }
    [McpServerTool(Name = "inventory_replace_tag", Destructive = true)]
    public async Task<object> ReplaceTag(Guid tagId, string name, string? description = null, TagColor? color = null, CancellationToken ct = default) { await Require(TagPermissions.UpdateKey, ct); return McpResult.Value(await tags.UpdateTagAsync(new(tagId, name, description, color))); }
    [McpServerTool(Name = "inventory_delete_tag", Destructive = true)]
    public async Task<object> DeleteTag(Guid tagId, CancellationToken ct = default) { await Require(TagPermissions.DeleteKey, ct); return McpResult.Done(await tags.DeleteTagAsync(new(tagId))); }

    private Task Require(string permission, CancellationToken ct) => authorization.RequireAsync(permission, ct);

    private static Uri? Uri(string? value, string field)
    {
        if (value is null) return null;
        return System.Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : throw new McpException($"'{field}' must be an absolute URI.");
    }
}
