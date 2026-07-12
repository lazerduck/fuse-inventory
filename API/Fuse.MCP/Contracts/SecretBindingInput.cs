using Fuse.Core.Models;
using ModelContextProtocol;

namespace Fuse.MCP;

public sealed class SecretBindingInput
{
    public SecretBindingKind Kind { get; init; } = SecretBindingKind.None;
    public string? PlainReference { get; init; }
    public Guid? ProviderId { get; init; }
    public string? SecretName { get; init; }
    public string? Version { get; init; }

    public SecretBinding ToModel() => Kind switch
    {
        SecretBindingKind.None => new(Kind, null, null),
        SecretBindingKind.PlainReference when !string.IsNullOrWhiteSpace(PlainReference) => new(Kind, PlainReference, null),
        SecretBindingKind.AzureKeyVault when ProviderId.HasValue && !string.IsNullOrWhiteSpace(SecretName) =>
            new(Kind, null, new AzureKeyVaultBinding(ProviderId.Value, SecretName, Version)),
        SecretBindingKind.PlainReference => throw new McpException("plainReference is required for a PlainReference secret binding."),
        SecretBindingKind.AzureKeyVault => throw new McpException("providerId and secretName are required for an AzureKeyVault secret binding."),
        _ => throw new McpException($"Unsupported secret binding kind '{Kind}'.")
    };
}
