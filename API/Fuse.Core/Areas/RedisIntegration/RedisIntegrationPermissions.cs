using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.RedisIntegration;

public sealed class RedisIntegrationPermissions : AreaPermissions
{
    public const string ReadKey = "redisintegrations:read";
    public const string CreateKey = "redisintegrations:create";
    public const string UpdateKey = "redisintegrations:update";
    public const string DeleteKey = "redisintegrations:delete";
    public const string ValidateKey = "redisintegrations:validate";
    public override string AreaName => "redisintegrations";
    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [new(ReadKey, IsAllowedInRestrictedEditing: true), new(CreateKey, IsAllowedInRestrictedEditing: false),
     new(UpdateKey, IsAllowedInRestrictedEditing: false), new(DeleteKey, IsAllowedInRestrictedEditing: false),
     new(ValidateKey, IsAllowedInRestrictedEditing: false)];
}
