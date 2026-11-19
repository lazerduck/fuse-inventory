using Fuse.Core.Responses;

namespace Fuse.Core.Areas.MongoDbIntegration;

public interface IMongoDbInspector
{
    Task<MongoDbConnectionTestResult> ValidateAsync(Commands.ValidateMongoDbConnection command, CancellationToken ct = default);
    Task<MongoDbIndexInventory> IndexesAsync(Models.MongoDbIntegration item, string collection, CancellationToken ct);
    Task<MongoDbDiscoveryResponse> DiscoverAsync(Models.MongoDbIntegration integration, IReadOnlyList<Models.Account> accounts, CancellationToken ct = default);
}
