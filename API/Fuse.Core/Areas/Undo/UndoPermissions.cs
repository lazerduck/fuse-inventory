using Fuse.Core.Areas.Security;

namespace Fuse.Core.Areas.Undo;

public sealed class UndoPermissions : AreaPermissions
{
    public const string ApplicationsUndoKey = "applications:undo";
    public const string AccountsUndoKey = "accounts:undo";
    public const string IdentitiesUndoKey = "identities:undo";
    public const string DataStoresUndoKey = "datastores:undo";
    public const string PlatformsUndoKey = "platforms:undo";
    public const string EnvironmentsUndoKey = "environments:undo";
    public const string ExternalResourcesUndoKey = "externalresources:undo";
    public const string MessageBrokersUndoKey = "messagebrokers:undo";
    public const string TagsUndoKey = "tags:undo";
    public const string PositionsUndoKey = "positions:undo";
    public const string ResponsibilitiesUndoKey = "responsibilities:undo";
    public const string RisksUndoKey = "risks:undo";
    public const string SecretProvidersUndoKey = "secretproviders:undo";
    public const string SqlIntegrationsUndoKey = "sqlintegrations:undo";
    public const string KumaIntegrationsUndoKey = "kumaintegrations:undo";
    public const string SecurityUndoKey = "security:undo";
    public const string ConfigurationUndoKey = "configuration:undo";

    public override string AreaName => "undo";

    public override IReadOnlyList<PermissionDescriptor> GetPermissionDescriptors() =>
    [
        new(ApplicationsUndoKey, IsAllowedInRestrictedEditing: false),
        new(AccountsUndoKey, IsAllowedInRestrictedEditing: false),
        new(IdentitiesUndoKey, IsAllowedInRestrictedEditing: false),
        new(DataStoresUndoKey, IsAllowedInRestrictedEditing: false),
        new(PlatformsUndoKey, IsAllowedInRestrictedEditing: false),
        new(EnvironmentsUndoKey, IsAllowedInRestrictedEditing: false),
        new(ExternalResourcesUndoKey, IsAllowedInRestrictedEditing: false),
        new(MessageBrokersUndoKey, IsAllowedInRestrictedEditing: false),
        new(TagsUndoKey, IsAllowedInRestrictedEditing: false),
        new(PositionsUndoKey, IsAllowedInRestrictedEditing: false),
        new(ResponsibilitiesUndoKey, IsAllowedInRestrictedEditing: false),
        new(RisksUndoKey, IsAllowedInRestrictedEditing: false),
        new(SecretProvidersUndoKey, IsAllowedInRestrictedEditing: false),
        new(SqlIntegrationsUndoKey, IsAllowedInRestrictedEditing: false),
        new(KumaIntegrationsUndoKey, IsAllowedInRestrictedEditing: false),
        new(SecurityUndoKey, IsAllowedInRestrictedEditing: false),
        new(ConfigurationUndoKey, IsAllowedInRestrictedEditing: false)
    ];
}
