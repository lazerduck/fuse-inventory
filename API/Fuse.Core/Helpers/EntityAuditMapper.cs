using Fuse.Core.Models;

namespace Fuse.Core.Helpers;

public static class EntityAuditMapper
{
    public static AuditArea ToAuditArea(EntityType entityType)
    {
        return entityType switch
        {
            EntityType.Application => AuditArea.Application,
            EntityType.Account => AuditArea.Account,
            EntityType.DataStore => AuditArea.DataStore,
            EntityType.Environment => AuditArea.Environment,
            EntityType.ExternalResource => AuditArea.ExternalResource,
            EntityType.Platform => AuditArea.Platform,
            EntityType.Tag => AuditArea.Tag,
            EntityType.KumaIntegration => AuditArea.KumaIntegration,
            EntityType.SecretProvider => AuditArea.SecretProvider,
            EntityType.SqlIntegration => AuditArea.SqlIntegration,
            EntityType.Position => AuditArea.Position,
            EntityType.ResponsibilityType => AuditArea.ResponsibilityType,
            EntityType.ResponsibilityAssignment => AuditArea.ResponsibilityAssignment,
            EntityType.Risk => AuditArea.Risk,
            EntityType.MessageBroker => AuditArea.Config,
            EntityType.Identity => AuditArea.Config,
            EntityType.SecurityUser => AuditArea.Security,
            EntityType.SecurityRole => AuditArea.Security,
            EntityType.PasswordGeneratorConfig => AuditArea.Config,
            _ => AuditArea.Config
        };
    }
}
