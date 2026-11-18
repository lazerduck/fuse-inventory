using Fuse.Core.Responses;

namespace Fuse.Core.Areas.PostgreSqlIntegration;

public interface IPostgreSqlInspector
{
    Task<PostgreSqlConnectionTestResult> ValidateAsync(Uri endpoint, string username, string? password, CancellationToken ct = default);
    Task<PostgreSqlDiscoveryResponse> DiscoverAsync(Models.PostgreSqlIntegration integration, IReadOnlyList<Models.Account> accounts, CancellationToken ct = default);
}
