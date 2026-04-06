using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Audit;

public sealed class AuditPermissions : AreaPermissions
{
    public const string ViewKey = "audit:view";

    public override string AreaName => "audit";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ViewKey, IsAllowedInRestrictedEditing: false)
    ];
}
