using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateSqlIntegration(
    string Name,
    Guid DataStoreId,
    string ConnectionString
);

public record UpdateSqlIntegration(
    Guid Id,
    string Name,
    Guid DataStoreId,
    string ConnectionString
);

public record DeleteSqlIntegration(Guid Id);

public record TestSqlConnection(
    string ConnectionString
);
