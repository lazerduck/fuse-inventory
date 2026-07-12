using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Fuse.Core.Commands;
using Fuse.Core.Models;
using ModelContextProtocol;

namespace Fuse.MCP;

public class ApplicationDependencyInput
{
    [Required] public Guid ApplicationId { get; init; }
    [Required] public Guid InstanceId { get; init; }
    [Required] public Guid TargetId { get; init; }
    [Required] public TargetKind TargetKind { get; init; }
    public int? Port { get; init; }
    [Required] public DependencyAuthKind AuthKind { get; init; }
    [Description("Required only when authKind is Account.")]
    public Guid? AccountId { get; init; }
    [Description("Required only when authKind is Identity.")]
    public Guid? IdentityId { get; init; }
    public DependencySeverity Severity { get; init; } = DependencySeverity.Full;

    public CreateApplicationDependency ToCreate() =>
        new(ApplicationId, InstanceId, TargetId, TargetKind, Port, AuthKind, AccountId, IdentityId, Severity);
}

public sealed class ReplaceApplicationDependencyInput : ApplicationDependencyInput
{
    [Required] public Guid DependencyId { get; init; }
    public UpdateApplicationDependency ToUpdate() =>
        new(ApplicationId, InstanceId, DependencyId, TargetId, TargetKind, Port, AuthKind, AccountId, IdentityId, Severity);
}

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

public class AccountInput
{
    [Required] public Guid TargetId { get; init; }
    [Required] public TargetKind TargetKind { get; init; }
    [Required] public AuthKind AuthKind { get; init; }
    public SecretBindingInput SecretBinding { get; init; } = new();
    public string? UserName { get; init; }
    public Dictionary<string, string>? Parameters { get; init; }
    public IReadOnlyList<Grant>? Grants { get; init; }
    public IReadOnlyList<Guid>? TagIds { get; init; }

    public CreateAccount ToCreate() => new(TargetId, TargetKind, AuthKind, SecretBinding.ToModel(), UserName, Parameters, Grants ?? [], TagIds?.ToHashSet() ?? []);
}

public sealed class ReplaceAccountInput : AccountInput
{
    [Required] public Guid Id { get; init; }
    public UpdateAccount ToUpdate(Account current) => new(Id, TargetId, TargetKind, AuthKind, SecretBinding.ToModel(), UserName, Parameters, Grants ?? current.Grants, TagIds?.ToHashSet() ?? current.TagIds);
}

public class IdentityInput
{
    [Required] public string Name { get; init; } = string.Empty;
    [Required] public IdentityKind Kind { get; init; }
    public string? Notes { get; init; }
    public Guid? OwnerInstanceId { get; init; }
    public IReadOnlyList<IdentityAssignment>? Assignments { get; init; }
    public IReadOnlyList<Guid>? TagIds { get; init; }

    public CreateIdentity ToCreate() => new(Name, Kind, Notes, OwnerInstanceId, Assignments ?? [], TagIds?.ToHashSet() ?? []);
}

public sealed class ReplaceIdentityInput : IdentityInput
{
    [Required] public Guid Id { get; init; }
    public UpdateIdentity ToUpdate(Identity current) => new(Id, Name, Kind, Notes, OwnerInstanceId, Assignments ?? current.Assignments, TagIds?.ToHashSet() ?? current.TagIds);
}
