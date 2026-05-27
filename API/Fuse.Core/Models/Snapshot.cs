using System;

namespace Fuse.Core.Models;

public record Snapshot(
    IReadOnlyList<Application> Applications,
    IReadOnlyList<DataStore> DataStores,
    IReadOnlyList<Platform> Platforms,
    IReadOnlyList<ExternalResource> ExternalResources,
    IReadOnlyList<Account> Accounts,
    IReadOnlyList<Identity> Identities,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<EnvironmentInfo> Environments,
    IReadOnlyList<KumaIntegration> KumaIntegrations,
    IReadOnlyList<SecretProvider> SecretProviders,
    IReadOnlyList<SqlIntegration> SqlIntegrations,
    IReadOnlyList<Position> Positions,
    IReadOnlyList<ResponsibilityType> ResponsibilityTypes,
    IReadOnlyList<ResponsibilityAssignment> ResponsibilityAssignments,
    IReadOnlyList<Risk> Risks,
    IReadOnlyList<MessageBroker> MessageBrokers,
    SecurityState Security,
    SecurityContext SecurityContext,
    PasswordGeneratorConfig? PasswordGeneratorConfig = null,
    AzureIntegrationManager? AzureIntegrationManager = null
)
{
    public Snapshot(
        IReadOnlyList<Application> Applications,
        IReadOnlyList<DataStore> DataStores,
        IReadOnlyList<Platform> Platforms,
        IReadOnlyList<ExternalResource> ExternalResources,
        IReadOnlyList<Account> Accounts,
        IReadOnlyList<Identity> Identities,
        IReadOnlyList<Tag> Tags,
        IReadOnlyList<EnvironmentInfo> Environments,
        IReadOnlyList<KumaIntegration> KumaIntegrations,
        IReadOnlyList<SecretProvider> SecretProviders,
        IReadOnlyList<SqlIntegration> SqlIntegrations,
        IReadOnlyList<Position> Positions,
        IReadOnlyList<ResponsibilityType> ResponsibilityTypes,
        IReadOnlyList<ResponsibilityAssignment> ResponsibilityAssignments,
        SecurityState Security,
        SecurityContext SecurityContext
    ) : this(
        Applications,
        DataStores,
        Platforms,
        ExternalResources,
        Accounts,
        Identities,
        Tags,
        Environments,
        KumaIntegrations,
        SecretProviders,
        SqlIntegrations,
        Positions,
        ResponsibilityTypes,
        ResponsibilityAssignments,
        Array.Empty<Risk>(),
        Array.Empty<MessageBroker>(),
        Security,
        SecurityContext
    )
    {
    }

    public Snapshot(
        IReadOnlyList<Application> Applications,
        IReadOnlyList<DataStore> DataStores,
        IReadOnlyList<Platform> Platforms,
        IReadOnlyList<ExternalResource> ExternalResources,
        IReadOnlyList<Account> Accounts,
        IReadOnlyList<Identity> Identities,
        IReadOnlyList<Tag> Tags,
        IReadOnlyList<EnvironmentInfo> Environments,
        IReadOnlyList<KumaIntegration> KumaIntegrations,
        IReadOnlyList<SecretProvider> SecretProviders,
        IReadOnlyList<SqlIntegration> SqlIntegrations,
        IReadOnlyList<Position> Positions,
        IReadOnlyList<ResponsibilityType> ResponsibilityTypes,
        IReadOnlyList<ResponsibilityAssignment> ResponsibilityAssignments
    ) : this(
        Applications,
        DataStores,
        Platforms,
        ExternalResources,
        Accounts,
        Identities,
        Tags,
        Environments,
        KumaIntegrations,
        SecretProviders,
        SqlIntegrations,
        Positions,
        ResponsibilityTypes,
        ResponsibilityAssignments,
        Array.Empty<Risk>(),
        Array.Empty<MessageBroker>(),
        new SecurityState(
            new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow),
            Array.Empty<SecurityUser>()
        ),
        new SecurityContext(SecurityPosture.Unrestricted, 
            Array.Empty<FuseRole>(),
            Array.Empty<FuseUser>(),
            Array.Empty<FuseApiKey>(),
            Array.Empty<Session>()
        )
    )
    {
    }

    public Snapshot(
        IReadOnlyList<Application> Applications,
        IReadOnlyList<DataStore> DataStores,
        IReadOnlyList<Platform> Platforms,
        IReadOnlyList<ExternalResource> ExternalResources,
        IReadOnlyList<Account> Accounts,
        IReadOnlyList<Identity> Identities,
        IReadOnlyList<Tag> Tags,
        IReadOnlyList<EnvironmentInfo> Environments,
        IReadOnlyList<KumaIntegration> KumaIntegrations,
        IReadOnlyList<SecretProvider> SecretProviders,
        IReadOnlyList<SqlIntegration> SqlIntegrations,
        IReadOnlyList<Position> Positions,
        IReadOnlyList<ResponsibilityType> ResponsibilityTypes,
        IReadOnlyList<ResponsibilityAssignment> ResponsibilityAssignments,
        IReadOnlyList<Risk> Risks,
        SecurityState Security,
        SecurityContext SecurityContext
    ) : this(
        Applications,
        DataStores,
        Platforms,
        ExternalResources,
        Accounts,
        Identities,
        Tags,
        Environments,
        KumaIntegrations,
        SecretProviders,
        SqlIntegrations,
        Positions,
        ResponsibilityTypes,
        ResponsibilityAssignments,
        Risks,
        Array.Empty<MessageBroker>(),
        Security,
        SecurityContext
    )
    {
    }
}
