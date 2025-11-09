namespace Fuse.Core.Models;

public record Platform
(
    Guid Id,
    string DisplayName,
    string? DnsName,
    string? Os,
    PlatformKind? Kind,
    string? IpAddress,
    string? Notes,
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public enum PlatformKind
{
    Server,
    Cluster,
    Serverless,
    ContainerHost
}