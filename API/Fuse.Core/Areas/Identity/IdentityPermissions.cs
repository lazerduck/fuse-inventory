using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Identity;

public sealed class IdentityPermissions : AreaPermissions
{
    public const string ReadKey = "identities:read";
    public const string CreateKey = "identities:create";
    public const string UpdateKey = "identities:update";
    public const string DeleteKey = "identities:delete";

    public override string AreaName => "identities";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false)
    ];
}
