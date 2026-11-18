using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.MongoDbIntegration;

public sealed class MongoDbIntegrationPermissions : AreaPermissions
{
    public const string ReadKey = "mongodbintegrations:read";
    public const string CreateKey = "mongodbintegrations:create";
    public const string UpdateKey = "mongodbintegrations:update";
    public const string DeleteKey = "mongodbintegrations:delete";
    public const string ValidateKey = "mongodbintegrations:validate";
    public override string AreaName => "mongodbintegrations";
    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [new(ReadKey, IsAllowedInRestrictedEditing: true), new(CreateKey, IsAllowedInRestrictedEditing: false),
     new(UpdateKey, IsAllowedInRestrictedEditing: false), new(DeleteKey, IsAllowedInRestrictedEditing: false),
     new(ValidateKey, IsAllowedInRestrictedEditing: false)];
}
