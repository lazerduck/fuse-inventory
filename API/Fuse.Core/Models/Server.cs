namespace Fuse.Core.Models;

public record Server
(
    Guid Id,
    string Name,
    string? Description,
    string Hostname,
    ServerOperatingSystem? OperatingSystem,
    Guid EnvironmentId,
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public enum ServerOperatingSystem
{
    Linux,
    Windows,
    MacOS,
    Other
}