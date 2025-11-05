using Microsoft.AspNetCore.Mvc;

namespace Fuse.Core.Commands;

public record CreateDataStore(
    string Name,
    string Kind,
    Guid EnvironmentId,
    Guid? ServerId,
    Uri? ConnectionUri,
    HashSet<Guid> TagIds
);

public record UpdateDataStore(
    Guid Id,
    string Name,
    string Kind,
    Guid EnvironmentId,
    Guid? ServerId,
    Uri? ConnectionUri,
    HashSet<Guid> TagIds
);

public record DeleteDataStore(
    Guid Id
);
