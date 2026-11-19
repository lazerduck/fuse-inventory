using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.ServiceBusIntegration;

public sealed class ServiceBusIntegrationPermissions : AreaPermissions
{
    public const string ReadKey = "servicebusintegrations:read";
    public const string CreateKey = "servicebusintegrations:create";
    public const string UpdateKey = "servicebusintegrations:update";
    public const string DeleteKey = "servicebusintegrations:delete";
    public const string ValidateKey = "servicebusintegrations:validate";
    public override string AreaName => "servicebusintegrations";
    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [new(ReadKey, IsAllowedInRestrictedEditing: true), new(CreateKey, IsAllowedInRestrictedEditing: false),
     new(UpdateKey, IsAllowedInRestrictedEditing: false), new(DeleteKey, IsAllowedInRestrictedEditing: false),
     new(ValidateKey, IsAllowedInRestrictedEditing: false)];
}
