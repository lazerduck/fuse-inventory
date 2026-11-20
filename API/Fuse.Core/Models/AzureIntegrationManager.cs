namespace Fuse.Core.Models;

public record AzureIntegrationManager(
    SecretProviderCredentials? ClientSecretCredentials,
    DateTime UpdatedAt,
    IReadOnlyList<string>? ExcludedResourceIds = null,
    IReadOnlyList<AzureCredentialProfile>? CredentialProfiles = null
);

public record AzureCredentialProfile(Guid Id, string Name, string ResourceId, Guid TenantId, Guid ClientId,
    string ProtectedSecret, bool DirectoryLookup)
{
    public static bool Allows(AzureIntegrationManager? manager, Guid? profileId, string resourceId, SecretProviderAuthMode mode) =>
        profileId is null || mode == SecretProviderAuthMode.ClientSecret &&
            manager?.CredentialProfiles?.Any(p => p.Id == profileId && string.Equals(p.ResourceId, resourceId.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) == true;
}
