using Fuse.Core.Models;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.CosmosDbIntegration;

public interface ICosmosDbIntegrationService
{
    Task<IReadOnlyList<CosmosDbConnection>> GetAllAsync(CancellationToken ct = default);
    Task<CosmosDbConnection?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<CosmosDbConnection>> CreateAsync(CreateCosmosDbIntegration command, CancellationToken ct = default);
    Task<Result<CosmosDbConnection>> UpdateAsync(UpdateCosmosDbIntegration command, CancellationToken ct = default);
    Task<Result> DeleteAsync(DeleteCosmosDbIntegration command, CancellationToken ct = default);
    Task<Result<CosmosDbConnectionTestResult>> ValidateAsync(ValidateCosmosDbConnection command, CancellationToken ct = default);
    Task<Result<CosmosDbConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default);
    Task<Result<CosmosDbDiscoveryResponse>> DiscoverAsync(Guid id, string? database, string? container, CancellationToken ct = default);
}
