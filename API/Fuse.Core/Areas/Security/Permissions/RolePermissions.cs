namespace Fuse.Core.Areas.Security.Permissions;

public sealed class RolePermissions : AreaPermissions
{
    public const string ReadKey = "roles:read";

    public const string CreateKey = "roles:create";

    public const string AssignKey = "roles:assign";

    public const string UpdateKey = "roles:update";

    public const string DeleteKey = "roles:delete";

    public override string AreaName => "roles";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(AssignKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false)
    ];
}