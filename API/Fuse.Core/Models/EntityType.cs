namespace Fuse.Core.Models;

/// <summary>
/// Represents the type of entity stored in a version snapshot
/// </summary>
public enum EntityType
{
    Application,
    Account,
    DataStore,
    Environment,
    ExternalResource,
    Platform,
    Tag,
    KumaIntegration,
    SecretProvider,
    SqlIntegration,
    Position,
    ResponsibilityType,
    ResponsibilityAssignment,
    Risk,
    MessageBroker,
    Identity,
    SecurityUser,
    SecurityRole,
    PasswordGeneratorConfig
}
