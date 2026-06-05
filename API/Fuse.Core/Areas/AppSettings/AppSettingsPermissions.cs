using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.AppSettings;

public sealed class AppSettingsPermissions : AreaPermissions
{
    public const string UpdateKey = "appsettings:update";

    public override string AreaName => "appsettings";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(UpdateKey, IsAllowedInRestrictedEditing: false)
    ];
}