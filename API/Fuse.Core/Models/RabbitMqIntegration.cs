namespace Fuse.Core.Models;

/// <summary>Private connection configuration. Public API responses must use RabbitMqIntegrationResponse.</summary>
public record RabbitMqIntegration(Guid Id, string Name, Guid MessageBrokerId, string VirtualHost, Uri Endpoint,
    string Username, string? Password, DateTime CreatedAt, DateTime UpdatedAt) : IPasswordConnection
{
    public BoundIntegrationPassword? PasswordBinding { get; init; }
    IPasswordConnection IPasswordConnection.WithPassword(string? value, BoundIntegrationPassword? binding, DateTime updatedAt) =>
        this with { Password = value, PasswordBinding = binding, UpdatedAt = updatedAt };
}
