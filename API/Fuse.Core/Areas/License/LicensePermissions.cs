using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.License;

public sealed class LicensePermissions : AreaPermissions
{
    public const string UpdateKey = "licenses:update";

    public override string AreaName => "licenses";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(UpdateKey, IsAllowedInRestrictedEditing: false)
    ];
}
