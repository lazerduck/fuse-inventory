using System.ComponentModel;
using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.DataStore;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.ExternalResource;
using Fuse.Core.Areas.Identity;
using Fuse.Core.Areas.MessageBroker;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.Position;
using Fuse.Core.Areas.Responsibility;
using Fuse.Core.Areas.Risk;
using Fuse.Core.Areas.Tag;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Fuse.MCP;

[McpServerToolType]
public sealed class InventoryReadTools(
    IAccountService accounts,
    IDataStoreService dataStores,
    IEnvironmentService environments,
    IExternalResourceService externalResources,
    IIdentityService identities,
    IMessageBrokerService messageBrokers,
    IPlatformService platforms,
    IPositionService positions,
    IResponsibilityTypeService responsibilityTypes,
    IResponsibilityAssignmentService responsibilities,
    IRiskService risks,
    ITagService tags,
    McpToolAuthorization authorization)
{
    [McpServerTool(Name = "inventory_list_items", ReadOnly = true)]
    [Description("List one kind of inventory item with its full editable definition. Use inventory_list_applications for applications.")]
    public async Task<object> ListItems(InventoryEntityType entityType, CancellationToken ct = default)
    {
        await authorization.RequireAsync(ReadPermission(entityType), ct);
        return entityType switch
        {
            InventoryEntityType.Account => await accounts.GetAccountsAsync(),
            InventoryEntityType.DataStore => await dataStores.GetDataStoresAsync(),
            InventoryEntityType.Environment => await environments.GetEnvironments(),
            InventoryEntityType.ExternalResource => await externalResources.GetExternalResourcesAsync(),
            InventoryEntityType.Identity => await identities.GetIdentitiesAsync(),
            InventoryEntityType.MessageBroker => await messageBrokers.GetMessageBrokersAsync(),
            InventoryEntityType.Platform => await platforms.GetPlatformsAsync(),
            InventoryEntityType.Position => await positions.GetPositionsAsync(),
            InventoryEntityType.ResponsibilityType => await responsibilityTypes.GetResponsibilityTypesAsync(),
            InventoryEntityType.Responsibility => await responsibilities.GetResponsibilityAssignmentsAsync(),
            InventoryEntityType.Risk => await risks.GetRisksAsync(),
            InventoryEntityType.Tag => await tags.GetTagsAsync(),
            _ => throw new McpException($"Unsupported inventory entity type '{entityType}'.")
        };
    }

    [McpServerTool(Name = "inventory_get_item", ReadOnly = true)]
    [Description("Get one inventory item by type and ID. Use inventory_get_application for applications.")]
    public async Task<object> GetItem(InventoryEntityType entityType, Guid id, CancellationToken ct = default)
    {
        await authorization.RequireAsync(ReadPermission(entityType), ct);
        object? item = entityType switch
        {
            InventoryEntityType.Account => await accounts.GetAccountByIdAsync(id),
            InventoryEntityType.DataStore => await dataStores.GetDataStoreByIdAsync(id),
            InventoryEntityType.Environment => (await environments.GetEnvironments()).FirstOrDefault(x => x.Id == id),
            InventoryEntityType.ExternalResource => await externalResources.GetExternalResourceByIdAsync(id),
            InventoryEntityType.Identity => await identities.GetIdentityByIdAsync(id),
            InventoryEntityType.MessageBroker => await messageBrokers.GetMessageBrokerByIdAsync(id),
            InventoryEntityType.Platform => await platforms.GetPlatformByIdAsync(id),
            InventoryEntityType.Position => await positions.GetPositionByIdAsync(id),
            InventoryEntityType.ResponsibilityType => await responsibilityTypes.GetResponsibilityTypeByIdAsync(id),
            InventoryEntityType.Responsibility => await responsibilities.GetResponsibilityAssignmentByIdAsync(id),
            InventoryEntityType.Risk => await risks.GetRiskByIdAsync(id),
            InventoryEntityType.Tag => await tags.GetTagByIdAsync(id),
            _ => null
        };
        return item ?? throw new McpException($"{entityType} '{id}' was not found.");
    }

    private static string ReadPermission(InventoryEntityType type) => type switch
    {
        InventoryEntityType.Account => AccountPermissions.ReadKey,
        InventoryEntityType.DataStore => DataStorePermissions.ReadKey,
        InventoryEntityType.Environment => EnvironmentPermissions.ReadKey,
        InventoryEntityType.ExternalResource => ExternalResourcePermissions.ReadKey,
        InventoryEntityType.Identity => IdentityPermissions.ReadKey,
        InventoryEntityType.MessageBroker => MessageBrokerPermissions.ReadKey,
        InventoryEntityType.Platform => PlatformPermissions.ReadKey,
        InventoryEntityType.Position => PositionPermissions.ReadKey,
        InventoryEntityType.ResponsibilityType or InventoryEntityType.Responsibility => ResponsibilityPermissions.ReadKey,
        InventoryEntityType.Risk => RiskPermissions.ReadKey,
        InventoryEntityType.Tag => TagPermissions.ReadKey,
        _ => throw new McpException($"Unsupported inventory entity type '{type}'.")
    };
}
