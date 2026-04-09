using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Tag;

public sealed class TagPermissions : AreaPermissions
{
    public const string ReadKey = "tags:read";
    public const string CreateKey = "tags:create";
    public const string UpdateKey = "tags:update";
    public const string DeleteKey = "tags:delete";

    public override string AreaName => "tags";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false)
    ];
}
