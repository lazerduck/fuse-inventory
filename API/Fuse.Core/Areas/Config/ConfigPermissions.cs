using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Config;

public sealed class ConfigPermissions : AreaPermissions
{
    public const string ExportKey = "config:export";
    public const string ImportKey = "config:import";

    public override string AreaName => "config";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ExportKey, IsAllowedInRestrictedEditing: false),
        new(ImportKey, IsAllowedInRestrictedEditing: false)
    ];
}