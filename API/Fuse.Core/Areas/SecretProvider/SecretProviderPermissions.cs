using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.SecretProvider;

public sealed class SecretProviderPermissions : AreaPermissions
{
    public const string ReadKey = "secretproviders:read";
    public const string CreateKey = "secretproviders:create";
    public const string UpdateKey = "secretproviders:update";
    public const string DeleteKey = "secretproviders:delete";
    public const string AppConfigReadKey = "secretproviders:appconfig:read";
    public const string AppConfigCreateKey = "secretproviders:appconfig:create";
    public const string AppConfigUpdateKey = "secretproviders:appconfig:update";
    public const string AppConfigDeleteKey = "secretproviders:appconfig:delete";
    public const string CreateSecretKey = "secretproviders:secrets:create";
    public const string RotateSecretKey = "secretproviders:secrets:rotate";
    public const string RevealSecretKey = "secretproviders:secrets:reveal";

    public override string AreaName => "secretproviders";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false),
        new(AppConfigReadKey, IsAllowedInRestrictedEditing: true),
        new(AppConfigCreateKey, IsAllowedInRestrictedEditing: false),
        new(AppConfigUpdateKey, IsAllowedInRestrictedEditing: false),
        new(AppConfigDeleteKey, IsAllowedInRestrictedEditing: false),
        new(CreateSecretKey, IsAllowedInRestrictedEditing: false),
        new(RotateSecretKey, IsAllowedInRestrictedEditing: false),
        new(RevealSecretKey, IsAllowedInRestrictedEditing: false)
    ];
}
