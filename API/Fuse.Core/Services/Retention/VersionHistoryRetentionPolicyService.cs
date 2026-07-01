using Fuse.Core.Areas.Activity;
using Fuse.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Fuse.Core.Services.Retention;

/// <summary>
/// Applies the configured version history retention policy.
/// This service contains the business logic for when and how to prune version history.
/// </summary>
public sealed class VersionHistoryRetentionPolicyService(
    IVersionHistoryService versionHistoryService,
    IFuseStore fuseStore,
    ILogger<VersionHistoryRetentionPolicyService> logger) : IVersionHistoryRetentionPolicyService
{
    /// <summary>
    /// Applies the configured version history retention policy.
    /// Fetches the keep count from configuration and prunes if enabled.
    /// </summary>
    public async Task ApplyRetentionPolicyAsync(CancellationToken ct = default)
    {
        try
        {
            // Get the version history retention setting
            var keepCount = await fuseStore.GetAsync(s => s.AppSettings?.VersionHistoryKeepCount ?? 0, ct);
            
            // Apply retention if enabled (keepCount > 0 means limited retention)
            if (keepCount > 0)
            {
                await versionHistoryService.PruneAllOldVersionsAsync(keepCount, ct);
            }
            // If keepCount <= 0, retention is disabled (unlimited) - nothing to do
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply version history retention policy");
            throw; // Let the caller handle the exception (worker will log and continue)
        }
    }
}

/// <summary>
/// Interface for the version history retention policy service.
/// Separates the retention policy logic from the worker timing mechanism.
/// </summary>
public interface IVersionHistoryRetentionPolicyService
{
    /// <summary>
    /// Applies the configured version history retention policy.
    /// </summary>
    Task ApplyRetentionPolicyAsync(CancellationToken ct = default);
}