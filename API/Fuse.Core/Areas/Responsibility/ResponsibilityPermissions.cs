using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Responsibility;

public sealed class ResponsibilityPermissions : AreaPermissions
{
    public const string ReadKey = "responsibilities:read";
    public const string CreateKey = "responsibilities:create";
    public const string UpdateKey = "responsibilities:update";
    public const string DeleteKey = "responsibilities:delete";

    public override string AreaName => "responsibilities";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false)
    ];
}
