using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Position;

public sealed class PositionPermissions : AreaPermissions
{
    public const string ReadKey = "positions:read";
    public const string CreateKey = "positions:create";
    public const string UpdateKey = "positions:update";
    public const string DeleteKey = "positions:delete";

    public override string AreaName => "positions";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false)
    ];
}
