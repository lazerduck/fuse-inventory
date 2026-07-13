using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Logging;

public sealed class LoggingPermissions : AreaPermissions
{
    public const string ReadKey = "logging:read";
    public const string PurgeKey = "logging:purge";

    public override string AreaName => "logging";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: false),
        new(PurgeKey, IsAllowedInRestrictedEditing: false)
    ];
}
