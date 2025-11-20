using Fuse.Core.Models;

namespace Fuse.Core.Responses;

public record SecretProviderResponse
(
    Guid Id,
    string Name,
    Uri VaultUri,
    SecretProviderAuthMode AuthMode,
    SecretProviderCapabilities Capabilities,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SecretValueResponse
(
    string Value
);

public record SecretMetadataResponse
(
    string Name,
    bool Enabled,
    DateTimeOffset? UpdatedOn,
    string? ContentType
);
