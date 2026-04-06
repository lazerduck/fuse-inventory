using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Environment;

public sealed class EnvironmentPermissions : AreaPermissions
{
    public const string ReadKey = "environments:read";
    public const string CreateKey = "environments:create";
    public const string UpdateKey = "environments:update";
    public const string DeleteKey = "environments:delete";
    public const string ApplyAutomationKey = "environments:automation:apply";

    public override string AreaName => "environments";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false),
        new(ApplyAutomationKey, IsAllowedInRestrictedEditing: false)
    ];
}
