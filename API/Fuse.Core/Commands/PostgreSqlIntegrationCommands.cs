namespace Fuse.Core.Commands;

public record CreatePostgreSqlIntegration(string Name, Guid DataStoreId, Uri Endpoint, string? Username, string? Password);
/// <summary>A null password retains the saved password; an empty password explicitly clears it.</summary>
public record UpdatePostgreSqlIntegration(Guid Id, string Name, Guid DataStoreId, Uri Endpoint, string? Username, string? Password);
public record DeletePostgreSqlIntegration(Guid Id);
public record ValidatePostgreSqlConnection(Uri Endpoint, string? Username, string? Password);
