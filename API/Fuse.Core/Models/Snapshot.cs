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
    SecurityState Security,
    PasswordGeneratorConfig? PasswordGeneratorConfig = null
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
        SecurityState Security
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
        Security
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
        new SecurityState(
            new SecuritySettings(SecurityLevel.FullyRestricted, DateTime.UtcNow),
            Array.Empty<SecurityUser>()
        )
    )
    {
    }
}