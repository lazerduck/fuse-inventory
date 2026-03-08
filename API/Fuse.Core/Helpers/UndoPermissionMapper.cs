using Fuse.Core.Models;

namespace Fuse.Core.Helpers;

public static class UndoPermissionMapper
{
    public static Permission ToPermission(EntityType entityType)
    {
        return entityType switch
        {
            EntityType.Application => Permission.ApplicationsUndo,
            EntityType.Account => Permission.AccountsUndo,
            EntityType.Identity => Permission.IdentitiesUndo,
            EntityType.DataStore => Permission.DataStoresUndo,
            EntityType.Platform => Permission.PlatformsUndo,
            EntityType.Environment => Permission.EnvironmentsUndo,
            EntityType.ExternalResource => Permission.ExternalResourcesUndo,
            EntityType.MessageBroker => Permission.MessageBrokersUndo,
            EntityType.Tag => Permission.TagsUndo,
            EntityType.Position => Permission.PositionsUndo,
            EntityType.ResponsibilityType => Permission.ResponsibilitiesUndo,
            EntityType.ResponsibilityAssignment => Permission.ResponsibilitiesUndo,
            EntityType.Risk => Permission.RisksUndo,
            EntityType.SecretProvider => Permission.SecretProvidersUndo,
            EntityType.SqlIntegration => Permission.SqlIntegrationsUndo,
            EntityType.KumaIntegration => Permission.KumaIntegrationsUndo,
            EntityType.SecurityUser => Permission.SecurityUndo,
            EntityType.SecurityRole => Permission.SecurityUndo,
            EntityType.PasswordGeneratorConfig => Permission.ConfigurationUndo,
            _ => Permission.ConfigurationUndo
        };
    }
}
