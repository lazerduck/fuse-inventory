namespace Fuse.Core.Areas.Security.Permissions;

public sealed class APIKeyPermissions : AreaPermissions
{
    public const string ReadKey = "apikey:read";

    public const string CreateKey = "apikey:create";

    public const string RegenerateKey = "apikey:regenerate";

    public const string DeleteKey = "apikey:delete";

    public override string AreaName => "apikey";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false, IgnorePosture: true),
        new(RegenerateKey, IsAllowedInRestrictedEditing: false, IgnorePosture: true),
        new(DeleteKey, IsAllowedInRestrictedEditing: false, IgnorePosture: true)
    ];
}