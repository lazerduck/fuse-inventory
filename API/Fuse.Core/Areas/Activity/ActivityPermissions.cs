using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Activity;

public sealed class ActivityPermissions : AreaPermissions
{
    public const string ReadKey = "activity:read";


    public override string AreaName => "activity";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey)
    ];
}