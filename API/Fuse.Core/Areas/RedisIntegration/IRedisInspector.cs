using Fuse.Core.Responses;

namespace Fuse.Core.Areas.RedisIntegration;

public interface IRedisInspector
{
    Task<RedisConnectionTestResult> ValidateAsync(Uri endpoint, string username, string? password, CancellationToken ct = default);
    Task<RedisDiscoveryResponse> DiscoverAsync(Models.RedisIntegration integration, IReadOnlyList<Models.Account> accounts, CancellationToken ct = default);
}
