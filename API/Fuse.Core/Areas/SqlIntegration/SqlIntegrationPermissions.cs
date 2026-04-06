using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.SqlIntegration;

public sealed class SqlIntegrationPermissions : AreaPermissions
{
    public const string ReadKey = "sqlintegrations:read";
    public const string CreateKey = "sqlintegrations:create";
    public const string UpdateKey = "sqlintegrations:update";
    public const string DeleteKey = "sqlintegrations:delete";
    public const string ApplyGrantsKey = "sqlintegrations:grants:apply";

    public override string AreaName => "sqlintegrations";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false),
        new(ApplyGrantsKey, IsAllowedInRestrictedEditing: false)
    ];
}
