using Fuse.Core.Responses;

namespace Fuse.Core.Interfaces;

/// <summary>
/// Interface for accessing and managing the SQL permissions cache.
/// The cache stores SQL permission overviews and individual account statuses
/// to provide fast load times.
/// </summary>
public interface ISqlPermissionsCache
{
    /// <summary>
    /// Gets the cached permissions overview for a SQL integration.
    /// Returns null if the integration has never been cached.
    /// </summary>
    /// <param name="integrationId">The SQL integration ID.</param>
    /// <returns>The cached overview or null if not available.</returns>
    CachedSqlPermissionsOverview? GetCachedOverview(Guid integrationId);

    /// <summary>
    /// Gets the cached SQL status for a specific account.
    /// Returns null if the account has never been cached.
    /// </summary>
    /// <param name="accountId">The account ID.</param>
    /// <returns>The cached status or null if not available.</returns>
    CachedAccountSqlStatus? GetCachedAccountStatus(Guid accountId);

    /// <summary>
    /// Triggers an immediate refresh of the permissions for a SQL integration.
    /// </summary>
    /// <param name="integrationId">The SQL integration ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refreshed overview or null if the integration does not exist.</returns>
    Task<CachedSqlPermissionsOverview?> RefreshIntegrationAsync(Guid integrationId, CancellationToken ct = default);

    /// <summary>
    /// Triggers an immediate refresh of the SQL status for a specific account.
    /// </summary>
    /// <param name="accountId">The account ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refreshed status or null if the account does not exist or is not associated with a SQL integration.</returns>
    Task<CachedAccountSqlStatus?> RefreshAccountAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Removes a specific integration from the cache.
    /// </summary>
    /// <param name="integrationId">The SQL integration ID to remove.</param>
    void InvalidateIntegration(Guid integrationId);

    /// <summary>
    /// Removes a specific account from the cache.
    /// </summary>
    /// <param name="accountId">The account ID to remove.</param>
    void InvalidateAccount(Guid accountId);
}

/// <summary>
/// Cached permissions overview for a SQL integration.
/// </summary>
public record CachedSqlPermissionsOverview(
    SqlIntegrationPermissionsOverviewResponse Overview,
    DateTime CachedAt
);

/// <summary>
/// Cached SQL status for a specific account.
/// </summary>
public record CachedAccountSqlStatus(
    AccountSqlStatusResponse Status,
    DateTime CachedAt
);
