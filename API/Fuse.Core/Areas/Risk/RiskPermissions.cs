using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Risk;

public sealed class RiskPermissions : AreaPermissions
{
    public const string ReadKey = "risks:read";
    public const string CreateKey = "risks:create";
    public const string UpdateKey = "risks:update";
    public const string DeleteKey = "risks:delete";
    public const string ApproveKey = "risks:approve";

    public override string AreaName => "risks";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false),
        new(ApproveKey, IsAllowedInRestrictedEditing: false)
    ];
}
