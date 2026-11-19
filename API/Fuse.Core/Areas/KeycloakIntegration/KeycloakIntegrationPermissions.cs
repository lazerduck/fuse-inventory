using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.KeycloakIntegration;

public sealed class KeycloakIntegrationPermissions : AreaPermissions
{
    public const string ReadKey = "keycloakintegrations:read";
    public const string CreateKey = "keycloakintegrations:create";
    public const string UpdateKey = "keycloakintegrations:update";
    public const string DeleteKey = "keycloakintegrations:delete";
    public const string ValidateKey = "keycloakintegrations:validate";
    public override string AreaName => "keycloakintegrations";
    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [new(ReadKey, IsAllowedInRestrictedEditing: true), new(CreateKey, IsAllowedInRestrictedEditing: false),
     new(UpdateKey, IsAllowedInRestrictedEditing: false), new(DeleteKey, IsAllowedInRestrictedEditing: false),
     new(ValidateKey, IsAllowedInRestrictedEditing: false)];
}
