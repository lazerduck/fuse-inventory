using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Platform;

public sealed class PlatformPermissions : AreaPermissions
{
    public const string ReadKey = "platforms:read";
    public const string CreateKey = "platforms:create";
    public const string UpdateKey = "platforms:update";
    public const string DeleteKey = "platforms:delete";
    public const string ManageAgentsKey = "platforms:manage-agents";
    public const string CheckUpdatesKey = "platforms:check-updates";

    public override string AreaName => "platforms";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false),
        new(ManageAgentsKey, IgnorePosture: true),
        new(CheckUpdatesKey, IgnorePosture: true)
    ];
}
