using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateSecretProvider(
    string Name,
    Uri VaultUri,
    SecretProviderAuthMode AuthMode,
    SecretProviderCredentials? Credentials,
    SecretProviderCapabilities Capabilities
);

public record UpdateSecretProvider(
    Guid Id,
    string Name,
    Uri VaultUri,
    SecretProviderAuthMode AuthMode,
    SecretProviderCredentials? Credentials,
    SecretProviderCapabilities Capabilities
);

public record DeleteSecretProvider(
    Guid Id
);

public record TestSecretProviderConnection(
    Uri VaultUri,
    SecretProviderAuthMode AuthMode,
    SecretProviderCredentials? Credentials
);

public record CreateSecret(
    Guid ProviderId,
    string SecretName,
    string SecretValue
);

public record RotateSecret(
    Guid ProviderId,
    string? SecretName,
    string NewSecretValue
);

public record RevealSecret(
    Guid ProviderId,
    string SecretName,
    string? Version
);
