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
using System.Text.Json;

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
    public async Task<object> ListItems(InventoryEntityType entityType, string? query = null,
        IReadOnlyList<Guid>? ids = null, IReadOnlyList<Guid>? tagIds = null,
        Guid? environmentId = null, Guid? platformId = null, int limit = 50,
        CancellationToken ct = default)
    {
        await authorization.RequireAsync(ReadPermission(entityType), ct);
        if (limit is < 1 or > 200) throw new McpException("limit must be between 1 and 200.");
        IEnumerable<object> items = entityType switch
        {
            InventoryEntityType.Account => (await accounts.GetAccountsAsync()).Cast<object>(),
            InventoryEntityType.DataStore => (await dataStores.GetDataStoresAsync()).Cast<object>(),
            InventoryEntityType.Environment => (await environments.GetEnvironments()).Cast<object>(),
            InventoryEntityType.ExternalResource => (await externalResources.GetExternalResourcesAsync()).Cast<object>(),
            InventoryEntityType.Identity => (await identities.GetIdentitiesAsync()).Cast<object>(),
            InventoryEntityType.MessageBroker => (await messageBrokers.GetMessageBrokersAsync()).Cast<object>(),
            InventoryEntityType.Platform => (await platforms.GetPlatformsAsync()).Cast<object>(),
            InventoryEntityType.Position => (await positions.GetPositionsAsync()).Cast<object>(),
            InventoryEntityType.ResponsibilityType => (await responsibilityTypes.GetResponsibilityTypesAsync()).Cast<object>(),
            InventoryEntityType.Responsibility => (await responsibilities.GetResponsibilityAssignmentsAsync()).Cast<object>(),
            InventoryEntityType.Risk => (await risks.GetRisksAsync()).Cast<object>(),
            InventoryEntityType.Tag => (await tags.GetTagsAsync()).Cast<object>(),
            _ => throw new McpException($"Unsupported inventory entity type '{entityType}'.")
        };
        var idSet = ids?.ToHashSet();
        var tagSet = tagIds?.ToHashSet();
        return items
            .Where(item => idSet is null || idSet.Contains(GetGuid(item, "Id") ?? Guid.Empty))
            .Where(item => tagSet is null || GetGuids(item, "TagIds").Any(tagSet.Contains))
            .Where(item => environmentId is null || GetGuid(item, "EnvironmentId") == environmentId)
            .Where(item => platformId is null || GetGuid(item, "PlatformId") == platformId || GetGuid(item, "Id") == platformId)
            .Where(item => string.IsNullOrWhiteSpace(query) || JsonSerializer.Serialize(item).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
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

    private static Guid? GetGuid(object item, string property) =>
        item.GetType().GetProperty(property)?.GetValue(item) as Guid?;

    private static IEnumerable<Guid> GetGuids(object item, string property) =>
        item.GetType().GetProperty(property)?.GetValue(item) as IEnumerable<Guid> ?? [];
}
