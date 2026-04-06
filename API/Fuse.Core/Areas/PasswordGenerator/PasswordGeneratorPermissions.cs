using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.PasswordGenerator;

public sealed class PasswordGeneratorPermissions : AreaPermissions
{
    public const string UpdateConfigKey = "passwordgenerator:config:update";

    public override string AreaName => "passwordgenerator";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(UpdateConfigKey, IsAllowedInRestrictedEditing: false)
    ];
}
