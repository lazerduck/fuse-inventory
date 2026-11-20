using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.PostgreSqlIntegration;

public sealed class PostgreSqlIntegrationPermissions : AreaPermissions
{
    public const string ReadKey = "postgresqlintegrations:read";
    public const string CreateKey = "postgresqlintegrations:create";
    public const string UpdateKey = "postgresqlintegrations:update";
    public const string DeleteKey = "postgresqlintegrations:delete";
    public const string ValidateKey = "postgresqlintegrations:validate";
    public const string ApplyGrantKey = "postgresqlintegrations:grants:apply";
    public override string AreaName => "postgresqlintegrations";
    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [new(ReadKey, IsAllowedInRestrictedEditing: true), new(CreateKey, IsAllowedInRestrictedEditing: false),
     new(UpdateKey, IsAllowedInRestrictedEditing: false), new(DeleteKey, IsAllowedInRestrictedEditing: false),
     new(ValidateKey, IsAllowedInRestrictedEditing: false), new(ApplyGrantKey, IsAllowedInRestrictedEditing: false, IgnorePosture: true)];
}
