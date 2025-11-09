using Microsoft.AspNetCore.Mvc;

namespace Fuse.Core.Commands;

public record CreateDataStore(
    string Name,
    string Kind,
    Guid EnvironmentId,
    Guid? PlatformId,
    Uri? ConnectionUri,
    HashSet<Guid>? TagIds = null
);

public record UpdateDataStore(
    Guid Id,
    string Name,
    string Kind,
    Guid EnvironmentId,
    Guid? PlatformId,
    Uri? ConnectionUri,
    HashSet<Guid>? TagIds = null
);

public record DeleteDataStore(
    Guid Id
);
