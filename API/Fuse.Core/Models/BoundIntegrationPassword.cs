namespace Fuse.Core.Models;

public record BoundIntegrationPassword(AzureKeyVaultBinding Reference, string Destination, Uri VaultUri);
public interface IPasswordConnection
{
    Guid Id { get; }
    Uri Endpoint { get; }
    string Username { get; }
    string? Password { get; }
    DateTime UpdatedAt { get; }
    BoundIntegrationPassword? PasswordBinding { get; }
    IPasswordConnection WithPassword(string? value, BoundIntegrationPassword? binding, DateTime updatedAt);
}
