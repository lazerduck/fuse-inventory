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
    SecurityState Security
);