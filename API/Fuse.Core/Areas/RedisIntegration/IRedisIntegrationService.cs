using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.RedisIntegration;

public interface IRedisIntegrationService
{
    Task<IReadOnlyList<RedisIntegrationResponse>> GetAllAsync(CancellationToken ct = default);
    Task<RedisIntegrationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<RedisIntegrationResponse>> CreateAsync(CreateRedisIntegration command, CancellationToken ct = default);
    Task<Result<RedisIntegrationResponse>> UpdateAsync(UpdateRedisIntegration command, CancellationToken ct = default);
    Task<Result> DeleteAsync(DeleteRedisIntegration command, CancellationToken ct = default);
    Task<Result<RedisConnectionTestResult>> ValidateAsync(ValidateRedisConnection command, CancellationToken ct = default);
    Task<Result<RedisConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default);
    Task<Result<RedisDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default);
}
