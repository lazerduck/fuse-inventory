namespace Fuse.Core.Models;

public record AppConfigurationEntry
(
    string Key,
    string? Value,
    string? Label,
    string? ContentType,
    DateTimeOffset? LastModified,
    bool IsLocked,
    bool IsKeyVaultReference,
    string? KeyVaultReferenceUri
);
