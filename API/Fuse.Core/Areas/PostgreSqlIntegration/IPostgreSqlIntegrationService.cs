using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.PostgreSqlIntegration;

public interface IPostgreSqlIntegrationService
{
    Task<IReadOnlyList<PostgreSqlIntegrationResponse>> GetAllAsync(CancellationToken ct = default);
    Task<PostgreSqlIntegrationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PostgreSqlIntegrationResponse>> CreateAsync(CreatePostgreSqlIntegration command, CancellationToken ct = default);
    Task<Result<PostgreSqlIntegrationResponse>> UpdateAsync(UpdatePostgreSqlIntegration command, CancellationToken ct = default);
    Task<Result> DeleteAsync(DeletePostgreSqlIntegration command, CancellationToken ct = default);
    Task<Result<PostgreSqlConnectionTestResult>> ValidateAsync(ValidatePostgreSqlConnection command, CancellationToken ct = default);
    Task<Result<PostgreSqlConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default);
    Task<Result<PostgreSqlDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default);
}
