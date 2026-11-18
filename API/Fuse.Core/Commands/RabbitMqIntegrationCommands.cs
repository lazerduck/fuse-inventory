namespace Fuse.Core.Commands;

public record CreateRabbitMqIntegration(string Name, Guid MessageBrokerId, string VirtualHost, Uri Endpoint, string? Username, string? Password);
/// <summary>A null password retains the saved password; an empty password explicitly clears it.</summary>
public record UpdateRabbitMqIntegration(Guid Id, string Name, Guid MessageBrokerId, string VirtualHost, Uri Endpoint, string? Username, string? Password);
public record DeleteRabbitMqIntegration(Guid Id);
public record ValidateRabbitMqConnection(string VirtualHost, Uri Endpoint, string? Username, string? Password);
