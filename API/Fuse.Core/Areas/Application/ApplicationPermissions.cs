using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Application;

public sealed class ApplicationPermissions : AreaPermissions
{
    public const string ReadKey = "application:read";
    public const string ReadInstanceAPIKey = "application:instance:api:read";
    public const string CreateKey = "application:create";
    public const string CreateInstanceKey = "application:instance:create";
    public const string UpdateKey = "application:update";
    public const string UpdateInstanceKey = "application:instance:update";
    public const string DeleteKey = "application:delete";
    public const string DeleteInstanceKey = "application:instance:delete";

    public override string AreaName => "application";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(ReadInstanceAPIKey, IsAllowedInRestrictedEditing: false, IgnorePosture:true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(CreateInstanceKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateInstanceKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false),
        new(DeleteInstanceKey, IsAllowedInRestrictedEditing: false)
    ];
}
