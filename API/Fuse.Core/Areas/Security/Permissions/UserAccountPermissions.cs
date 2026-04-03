namespace Fuse.Core.Areas.Security.Permissions;

public sealed class UserAccountPermissions : AreaPermissions
{
    public const string ReadKey = "useraccount:read";

    public const string CreateKey = "useraccount:create";

    public const string UpdateKey = "useraccount:update";

    public const string DeleteKey = "useraccount:delete";

    public const string ResetPasswordKey = "useraccount:resetpassword";

    public override string AreaName => "useraccount";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() => 
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false, IgnorePosture: true),
        new(UpdateKey, IsAllowedInRestrictedEditing: false, IgnorePosture: true),
        new(DeleteKey, IsAllowedInRestrictedEditing: false, IgnorePosture: true),
        new(ResetPasswordKey, IsAllowedInRestrictedEditing: false, IgnorePosture: true)
    ];
}