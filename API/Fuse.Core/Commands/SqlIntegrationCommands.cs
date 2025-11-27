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

/// <summary>
/// Specifies how the password is obtained for bulk SQL account creation.
/// </summary>
public enum BulkPasswordSource
{
    /// <summary>
    /// Retrieve password from the linked Secret Provider (e.g., Azure Key Vault) for each account.
    /// Accounts without a Secret Provider link will be skipped.
    /// </summary>
    SecretProvider
}

/// <summary>
/// Request body for bulk resolve operations (creating missing accounts and resolving drift).
/// </summary>
public record BulkResolveRequest(
    BulkPasswordSource PasswordSource
);

/// <summary>
/// Command to bulk resolve all resolvable accounts in a SQL integration.
/// Creates missing accounts and applies missing grants.
/// </summary>
public record BulkResolve(
    Guid IntegrationId,
    BulkPasswordSource PasswordSource
);
