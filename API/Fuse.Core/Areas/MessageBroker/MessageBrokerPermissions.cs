using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.MessageBroker;

public sealed class MessageBrokerPermissions : AreaPermissions
{
    public const string ReadKey = "messagebrokers:read";
    public const string CreateKey = "messagebrokers:create";
    public const string UpdateKey = "messagebrokers:update";
    public const string DeleteKey = "messagebrokers:delete";

    public override string AreaName => "messagebrokers";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ReadKey, IsAllowedInRestrictedEditing: true),
        new(CreateKey, IsAllowedInRestrictedEditing: false),
        new(UpdateKey, IsAllowedInRestrictedEditing: false),
        new(DeleteKey, IsAllowedInRestrictedEditing: false)
    ];
}
