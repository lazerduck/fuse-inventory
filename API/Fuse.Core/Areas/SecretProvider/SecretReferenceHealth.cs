using Fuse.Core.Models;

namespace Fuse.Core.Areas.SecretProvider;

public record KeyVaultVersion(string Version, DateTimeOffset? Created, DateTimeOffset? Expires, bool? Enabled);
public record KeyVaultVersions(string Visibility, IReadOnlyList<KeyVaultVersion> Versions);
public record SecretConsumer(Guid ApplicationId, string Application, Guid InstanceId, string Source, string? PinnedVersion);
public record SecretReferenceHealth(string Name, string? PinnedVersion, KeyVaultVersions Metadata, IReadOnlyList<SecretConsumer> Consumers);

public static class SecretReferenceConsumers
{
    public static IReadOnlyList<SecretConsumer> Find(Snapshot s, Guid vault, string name, bool canReadAccounts,
        Guid? configuration = null, string? key = null, string? version = null)
    {
        bool Matches(SecretBinding? binding) => binding is { Kind: SecretBindingKind.AzureKeyVault, AzureKeyVault: {} b } &&
            b.ProviderId == vault && b.SecretName.Equals(name, StringComparison.OrdinalIgnoreCase);
        var accounts = canReadAccounts ? s.Accounts.Where(a => Matches(a.SecretBinding)).ToDictionary(a => a.Id) : [];
        var rows = new List<SecretConsumer>();
        foreach (var app in s.Applications)
            foreach (var instance in app.Instances)
            {
                if (Matches(instance.ApiKey)) Add("Instance API key", instance.ApiKey!.AzureKeyVault!.Version);
                foreach (var id in instance.Dependencies.Select(d => d.AccountId).OfType<Guid>().Distinct())
                    if (accounts.TryGetValue(id, out var account)) Add("Dependency account " + (account.UserName ?? id.ToString()), account.SecretBinding.AzureKeyVault!.Version);
                if (configuration.HasValue && instance.AppConfigurationProviderId == configuration && key is not null &&
                    key.EndsWith(instance.AppConfigurationKeySuffix?.Trim() ?? "", StringComparison.OrdinalIgnoreCase))
                    Add("Configured key scope: " + key, version);

                void Add(string source, string? pinned) => rows.Add(new(app.Id, app.Name, instance.Id, source, pinned));
            }
        return rows.Distinct().OrderBy(r => r.Application).ThenBy(r => r.InstanceId).ToArray();
    }
}
