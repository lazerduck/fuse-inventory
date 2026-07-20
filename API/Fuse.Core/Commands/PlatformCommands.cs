using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record PlatformNodeInput(
    Guid? Id,
    string DisplayName,
    string? DnsName = null,
    string? Os = null,
    IReadOnlyList<string>? IpAddresses = null,
    string? Notes = null
);

public record CreatePlatform(
    string DisplayName,
    string? DnsName = null,
    string? Os = null,
    PlatformKind? Kind = null,
    IReadOnlyList<string>? IpAddresses = null,
    string? Notes = null,
    HashSet<Guid>? TagIds = null,
    IReadOnlyList<PlatformNodeInput>? Nodes = null
);

public record UpdatePlatform(
    Guid Id,
    string DisplayName,
    string? DnsName = null,
    string? Os = null,
    PlatformKind? Kind = null,
    IReadOnlyList<string>? IpAddresses = null,
    string? Notes = null,
    HashSet<Guid>? TagIds = null,
    IReadOnlyList<PlatformNodeInput>? Nodes = null
);

public record DeletePlatform(
    Guid Id
);
