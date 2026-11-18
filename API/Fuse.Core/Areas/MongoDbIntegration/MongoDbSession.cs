using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
namespace Fuse.Core.Areas.MongoDbIntegration;

public interface IMongoDbSession : IDisposable
{
    Task<BsonDocument> RunAsync(string database, BsonDocument command, CancellationToken ct);
}
public interface IMongoDbSessionFactory
{
    IMongoDbSession Open(Commands.ValidateMongoDbConnection command);
}
public sealed class MongoDbSessionFactory : IMongoDbSessionFactory
{
    public IMongoDbSession Open(Commands.ValidateMongoDbConnection command) => new Session(command);
    private sealed class Session(Commands.ValidateMongoDbConnection command) : IMongoDbSession
    {
        private readonly MongoClient client = new(MongoDbConnectionSettings.Build(command));
        public Task<BsonDocument> RunAsync(string database, BsonDocument command, CancellationToken ct) =>
            client.GetDatabase(database).RunCommandAsync<BsonDocument>(command, cancellationToken: ct);
        public void Dispose()
        {
            var cluster = client.Cluster;
            client.Dispose();
            ClusterRegistry.Instance.UnregisterAndDisposeCluster(cluster);
        }
    }
}
