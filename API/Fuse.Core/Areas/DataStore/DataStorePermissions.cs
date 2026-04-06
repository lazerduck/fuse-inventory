using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.DataStore;

public sealed class DataStorePermissions : AreaPermissions
{
    public const string ReadKey = "datastores:read";
    public const string CreateKey = "datastores:create";
    public const string UpdateKey = "datastores:update";
    public const string DeleteKey = "datastores:delete";

    public override string AreaName => "datastores";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false)
    ];
}