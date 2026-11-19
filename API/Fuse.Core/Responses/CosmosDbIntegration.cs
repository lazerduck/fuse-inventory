namespace Fuse.Core.Responses;

public record CosmosDbConnectionTestResult(bool IsSuccessful, string Status);
public record CosmosDbPartitionKey(string? Kind, int? Version, IReadOnlyList<string> Paths);
public record CosmosDbIndexingPolicy(string? Mode, bool? Automatic, IReadOnlyList<string> IncludedPaths,
    IReadOnlyList<string> ExcludedPaths, string PolicyJson);
public record CosmosDbContainer(string Name, CosmosDbPartitionKey? PartitionKey, CosmosDbIndexingPolicy? IndexingPolicy);
public record CosmosDbDatabase(string Name, string ContainerVisibility, IReadOnlyList<CosmosDbContainer> Containers);
public record CosmosDbDiscoveryResponse(string AccountResourceId, DateTimeOffset ObservedAt, string Visibility,
    IReadOnlyList<CosmosDbDatabase> Databases);
