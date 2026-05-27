namespace Fuse.Core.Models;

public record AzureIntegrationManager(
    SecretProviderCredentials? ClientSecretCredentials,
    DateTime UpdatedAt
);
