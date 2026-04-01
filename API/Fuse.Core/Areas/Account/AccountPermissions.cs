using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Account;

public sealed class AccountPermissions : AreaPermissions
{
    public const string ReadKey = "accounts:read";
    public const string CreateKey = "accounts:create";
    public const string UpdateKey = "accounts:update";
    public const string DeleteKey = "accounts:delete";

    public override string AreaName => "accounts";

    public override IReadOnlyList<string> GetPermissions() =>
    [
        ReadKey,
        CreateKey,
        UpdateKey,
        DeleteKey
    ];

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false)
    ];
}
