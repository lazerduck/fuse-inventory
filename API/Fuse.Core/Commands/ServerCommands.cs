using Microsoft.AspNetCore.Mvc;
using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateServer(
    string Name,
    string Hostname,
    ServerOperatingSystem? OperatingSystem,
    Guid EnvironmentId,
    HashSet<Guid> TagIds
);

public record UpdateServer(
    Guid Id,
    string Name,
    string Hostname,
    ServerOperatingSystem? OperatingSystem,
    Guid EnvironmentId,
    HashSet<Guid> TagIds
);

public record DeleteServer(
    Guid Id
);
