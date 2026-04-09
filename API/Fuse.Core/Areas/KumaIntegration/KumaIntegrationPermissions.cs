using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.KumaIntegration;

public sealed class KumaIntegrationPermissions : AreaPermissions
{
    public const string ReadKey = "kumaintegrations:read";
    public const string CreateKey = "kumaintegrations:create";
    public const string UpdateKey = "kumaintegrations:update";
    public const string DeleteKey = "kumaintegrations:delete";

    public override string AreaName => "kumaintegrations";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false)
    ];
}
