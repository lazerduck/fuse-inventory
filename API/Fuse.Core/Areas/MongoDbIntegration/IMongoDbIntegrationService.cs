using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.MongoDbIntegration;

public interface IMongoDbIntegrationService
{
    Task<IReadOnlyList<MongoDbIntegrationResponse>> GetAllAsync(CancellationToken ct = default);
    Task<MongoDbIntegrationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<MongoDbIntegrationResponse>> CreateAsync(CreateMongoDbIntegration command, CancellationToken ct = default);
    Task<Result<MongoDbIntegrationResponse>> UpdateAsync(UpdateMongoDbIntegration command, CancellationToken ct = default);
    Task<Result> DeleteAsync(DeleteMongoDbIntegration command, CancellationToken ct = default);
    Task<Result<MongoDbConnectionTestResult>> ValidateAsync(ValidateMongoDbConnection command, CancellationToken ct = default);
    Task<Result<MongoDbConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default);
    Task<Result<MongoDbDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default);
}
