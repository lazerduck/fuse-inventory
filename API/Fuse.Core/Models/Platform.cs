namespace Fuse.Core.Models;

public record Platform
(
    Guid Id,
    string DisplayName,
    string? DnsName,
    string? Os,
    PlatformKind? Kind,
    IReadOnlyList<string> IpAddresses,
    string? Notes,
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<PlatformNode>? Nodes = null
);

public record PlatformNode(
    Guid Id,
    string DisplayName,
    string? DnsName,
    string? Os,
    IReadOnlyList<string> IpAddresses,
    string? Notes
);

public enum PlatformKind
{
    Server,
    Cluster,
    Serverless,
    ContainerHost
}
