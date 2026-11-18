namespace Fuse.Core.Commands;

public record CreateMongoDbIntegration(string Name, Guid DataStoreId, Uri Endpoint, string? Username, string? Password, string AuthenticationDatabase, bool UseTls = false);
/// <summary>A null password retains the saved password; an empty password explicitly clears it.</summary>
public record UpdateMongoDbIntegration(Guid Id, string Name, Guid DataStoreId, Uri Endpoint, string? Username, string? Password, string AuthenticationDatabase, bool UseTls = false);
public record DeleteMongoDbIntegration(Guid Id);
public record ValidateMongoDbConnection(Uri Endpoint, string? Username, string? Password, string AuthenticationDatabase, bool UseTls = false);
