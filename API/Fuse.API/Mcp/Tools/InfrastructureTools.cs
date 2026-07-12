using Fuse.Core.Areas.DataStore;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.ExternalResource;
using Fuse.Core.Areas.MessageBroker;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.Tag;
using Fuse.Core.Commands;
using ModelContextProtocol.Server;

namespace Fuse.API.Mcp;

[McpServerToolType]
public sealed class InfrastructureTools(
    IDataStoreService dataStores, IEnvironmentService environments,
    IExternalResourceService externalResources, IMessageBrokerService messageBrokers,
    IPlatformService platforms, ITagService tags, McpToolAuthorization authorization)
{
    [McpServerTool(Name = "inventory_create_datastore", Destructive = false)]
    public async Task<object> CreateDataStore(CreateDataStore command, CancellationToken ct = default) { await Require(DataStorePermissions.CreateKey, ct); return McpResult.Value(await dataStores.CreateDataStoreAsync(command)); }
    [McpServerTool(Name = "inventory_replace_datastore", Destructive = true)]
    public async Task<object> ReplaceDataStore(UpdateDataStore command, CancellationToken ct = default) { await Require(DataStorePermissions.UpdateKey, ct); return McpResult.Value(await dataStores.UpdateDataStoreAsync(command)); }
    [McpServerTool(Name = "inventory_delete_datastore", Destructive = true)]
    public async Task<object> DeleteDataStore(Guid dataStoreId, CancellationToken ct = default) { await Require(DataStorePermissions.DeleteKey, ct); return McpResult.Done(await dataStores.DeleteDataStoreAsync(new(dataStoreId))); }

    [McpServerTool(Name = "inventory_create_environment", Destructive = false)]
    public async Task<object> CreateEnvironment(CreateEnvironment command, CancellationToken ct = default) { await Require(EnvironmentPermissions.CreateKey, ct); return McpResult.Value(await environments.CreateEnvironment(command)); }
    [McpServerTool(Name = "inventory_replace_environment", Destructive = true)]
    public async Task<object> ReplaceEnvironment(UpdateEnvironment command, CancellationToken ct = default) { await Require(EnvironmentPermissions.UpdateKey, ct); return McpResult.Value(await environments.UpdateEnvironment(command)); }
    [McpServerTool(Name = "inventory_delete_environment", Destructive = true)]
    public async Task<object> DeleteEnvironment(Guid environmentId, CancellationToken ct = default) { await Require(EnvironmentPermissions.DeleteKey, ct); return McpResult.Done(await environments.DeleteEnvironmentAsync(new(environmentId))); }
    [McpServerTool(Name = "inventory_apply_environment_automation", Destructive = true)]
    public async Task<object> ApplyAutomation(ApplyEnvironmentAutomation command, CancellationToken ct = default) { await Require(EnvironmentPermissions.ApplyAutomationKey, ct); return McpResult.Value(await environments.ApplyEnvironmentAutomationAsync(command)); }

    [McpServerTool(Name = "inventory_create_external_resource", Destructive = false)]
    public async Task<object> CreateExternalResource(CreateExternalResource command, CancellationToken ct = default) { await Require(ExternalResourcePermissions.CreateKey, ct); return McpResult.Value(await externalResources.CreateExternalResourceAsync(command)); }
    [McpServerTool(Name = "inventory_replace_external_resource", Destructive = true)]
    public async Task<object> ReplaceExternalResource(UpdateExternalResource command, CancellationToken ct = default) { await Require(ExternalResourcePermissions.UpdateKey, ct); return McpResult.Value(await externalResources.UpdateExternalResourceAsync(command)); }
    [McpServerTool(Name = "inventory_delete_external_resource", Destructive = true)]
    public async Task<object> DeleteExternalResource(Guid externalResourceId, CancellationToken ct = default) { await Require(ExternalResourcePermissions.DeleteKey, ct); return McpResult.Done(await externalResources.DeleteExternalResourceAsync(new(externalResourceId))); }

    [McpServerTool(Name = "inventory_create_message_broker", Destructive = false)]
    public async Task<object> CreateMessageBroker(CreateMessageBroker command, CancellationToken ct = default) { await Require(MessageBrokerPermissions.CreateKey, ct); return McpResult.Value(await messageBrokers.CreateMessageBrokerAsync(command)); }
    [McpServerTool(Name = "inventory_replace_message_broker", Destructive = true)]
    public async Task<object> ReplaceMessageBroker(UpdateMessageBroker command, CancellationToken ct = default) { await Require(MessageBrokerPermissions.UpdateKey, ct); return McpResult.Value(await messageBrokers.UpdateMessageBrokerAsync(command)); }
    [McpServerTool(Name = "inventory_delete_message_broker", Destructive = true)]
    public async Task<object> DeleteMessageBroker(Guid messageBrokerId, CancellationToken ct = default) { await Require(MessageBrokerPermissions.DeleteKey, ct); return McpResult.Done(await messageBrokers.DeleteMessageBrokerAsync(new(messageBrokerId))); }

    [McpServerTool(Name = "inventory_create_platform", Destructive = false)]
    public async Task<object> CreatePlatform(CreatePlatform command, CancellationToken ct = default) { await Require(PlatformPermissions.CreateKey, ct); return McpResult.Value(await platforms.CreatePlatformAsync(command)); }
    [McpServerTool(Name = "inventory_replace_platform", Destructive = true)]
    public async Task<object> ReplacePlatform(UpdatePlatform command, CancellationToken ct = default) { await Require(PlatformPermissions.UpdateKey, ct); return McpResult.Value(await platforms.UpdatePlatformAsync(command)); }
    [McpServerTool(Name = "inventory_delete_platform", Destructive = true)]
    public async Task<object> DeletePlatform(Guid platformId, CancellationToken ct = default) { await Require(PlatformPermissions.DeleteKey, ct); return McpResult.Done(await platforms.DeletePlatformAsync(new(platformId))); }

    [McpServerTool(Name = "inventory_create_tag", Destructive = false)]
    public async Task<object> CreateTag(CreateTag command, CancellationToken ct = default) { await Require(TagPermissions.CreateKey, ct); return McpResult.Value(await tags.CreateTagAsync(command)); }
    [McpServerTool(Name = "inventory_replace_tag", Destructive = true)]
    public async Task<object> ReplaceTag(UpdateTag command, CancellationToken ct = default) { await Require(TagPermissions.UpdateKey, ct); return McpResult.Value(await tags.UpdateTagAsync(command)); }
    [McpServerTool(Name = "inventory_delete_tag", Destructive = true)]
    public async Task<object> DeleteTag(Guid tagId, CancellationToken ct = default) { await Require(TagPermissions.DeleteKey, ct); return McpResult.Done(await tags.DeleteTagAsync(new(tagId))); }

    private Task Require(string permission, CancellationToken ct) => authorization.RequireAsync(permission, ct);
}
