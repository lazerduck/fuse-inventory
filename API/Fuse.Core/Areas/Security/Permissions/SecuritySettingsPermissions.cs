namespace Fuse.Core.Areas.Security.Permissions;

public sealed class SecuritySettingsPermissions : AreaPermissions
{
    public const string UpdateSettingsKey = "security:settings:update";

    public override string AreaName => "security";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(UpdateSettingsKey, IsAllowedInRestrictedEditing: false, IgnorePosture: true)
    ];
}
