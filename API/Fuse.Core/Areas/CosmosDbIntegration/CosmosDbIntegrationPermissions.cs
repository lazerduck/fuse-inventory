using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.CosmosDbIntegration;

public sealed class CosmosDbIntegrationPermissions : AreaPermissions
{
    public const string ReadKey = "cosmosdbintegrations:read";
    public const string CreateKey = "cosmosdbintegrations:create";
    public const string UpdateKey = "cosmosdbintegrations:update";
    public const string DeleteKey = "cosmosdbintegrations:delete";
    public const string ValidateKey = "cosmosdbintegrations:validate";
    public override string AreaName => "cosmosdbintegrations";
    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [new(ReadKey, IsAllowedInRestrictedEditing: true), new(CreateKey, IsAllowedInRestrictedEditing: false),
     new(UpdateKey, IsAllowedInRestrictedEditing: false), new(DeleteKey, IsAllowedInRestrictedEditing: false),
     new(ValidateKey, IsAllowedInRestrictedEditing: false)];
}
