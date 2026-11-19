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
    SecurityContext SecurityContext,
    AppSettings AppSettings,
    PasswordGeneratorConfig? PasswordGeneratorConfig = null,
    AzureIntegrationManager? AzureIntegrationManager = null,
    LicenseState? License = null
)
{
    public IReadOnlyList<RabbitMqIntegration> RabbitMqIntegrations { get; init; } = Array.Empty<RabbitMqIntegration>();

    public IReadOnlyList<RedisIntegration> RedisIntegrations { get; init; } = Array.Empty<RedisIntegration>();
    public IReadOnlyList<PostgreSqlIntegration> PostgreSqlIntegrations { get; init; } = Array.Empty<PostgreSqlIntegration>();
    public IReadOnlyList<MongoDbIntegration> MongoDbIntegrations { get; init; } = Array.Empty<MongoDbIntegration>();
    public IReadOnlyList<KeycloakIntegration> KeycloakIntegrations { get; init; } = Array.Empty<KeycloakIntegration>();

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
        SecurityContext,
        new AppSettings()
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
        new SecurityContext(SecurityPosture.Unrestricted, 
            Array.Empty<FuseRole>(),
            Array.Empty<FuseUser>(),
            Array.Empty<FuseApiKey>(),
            Array.Empty<Session>()
        ),
        new AppSettings()
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
        SecurityContext SecurityContext,
        AppSettings AppSettings
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
        SecurityContext,
        AppSettings
    )
    {
    }
}
