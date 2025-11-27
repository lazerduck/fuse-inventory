using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateSqlIntegration(
    string Name,
    Guid DataStoreId,
    string ConnectionString
);

public record UpdateSqlIntegration(
    Guid Id,
    string Name,
    Guid DataStoreId,
    string ConnectionString
);

public record DeleteSqlIntegration(Guid Id);

public record TestSqlConnection(
    string ConnectionString
);

public record ResolveDrift(
    Guid IntegrationId,
    Guid AccountId
);

/// <summary>
/// Specifies how the password is obtained for SQL account creation.
/// </summary>
public enum PasswordSource
{
    /// <summary>
    /// Retrieve password from the linked Secret Provider (e.g., Azure Key Vault).
    /// </summary>
    SecretProvider,
    
    /// <summary>
    /// Password is provided manually by the user.
    /// </summary>
    Manual,
    
    /// <summary>
    /// Create a new secret in the Secret Provider with the provided password.
    /// </summary>
    NewSecret
}

/// <summary>
/// Request body for creating a SQL account.
/// </summary>
public record CreateSqlAccountRequest(
    PasswordSource PasswordSource,
    string? Password
);

/// <summary>
/// Command to create a SQL login and user for an account that exists in Fuse but not in SQL.
/// </summary>
public record CreateSqlAccount(
    Guid IntegrationId,
    Guid AccountId,
    PasswordSource PasswordSource,
    string? Password
);
