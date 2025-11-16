namespace Fuse.Core.Models;

public record Snapshot(
    IReadOnlyList<Application> Applications,
    IReadOnlyList<DataStore> DataStores,
    IReadOnlyList<Platform> Platforms,
    IReadOnlyList<ExternalResource> ExternalResources,
    IReadOnlyList<Account> Accounts,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<EnvironmentInfo> Environments,
    IReadOnlyList<KumaIntegration> KumaIntegrations,
    SecurityState Security
);