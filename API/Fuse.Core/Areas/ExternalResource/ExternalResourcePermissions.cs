using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.ExternalResource;

public sealed class ExternalResourcePermissions : AreaPermissions
{
    public const string ReadKey = "externalresources:read";
    public const string CreateKey = "externalresources:create";
    public const string UpdateKey = "externalresources:update";
    public const string DeleteKey = "externalresources:delete";

    public override string AreaName => "externalresources";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false)
    ];
}
