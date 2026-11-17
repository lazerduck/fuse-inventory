using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.RabbitMqIntegration;

public sealed class RabbitMqIntegrationPermissions : AreaPermissions
{
    public const string ReadKey = "rabbitmqintegrations:read";
    public const string CreateKey = "rabbitmqintegrations:create";
    public const string UpdateKey = "rabbitmqintegrations:update";
    public const string DeleteKey = "rabbitmqintegrations:delete";
    public const string ValidateKey = "rabbitmqintegrations:validate";
    public override string AreaName => "rabbitmqintegrations";
    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [new(ReadKey, IsAllowedInRestrictedEditing: true), new(CreateKey, IsAllowedInRestrictedEditing: false),
     new(UpdateKey, IsAllowedInRestrictedEditing: false), new(DeleteKey, IsAllowedInRestrictedEditing: false),
     new(ValidateKey, IsAllowedInRestrictedEditing: false)];
}
