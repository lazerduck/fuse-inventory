using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreatePlatform(
    string DisplayName,
    string? DnsName = null,
    string? Os = null,
    PlatformKind? Kind = null,
    string? IpAddress = null,
    string? Notes = null,
    HashSet<Guid>? TagIds = null
);

public record UpdatePlatform(
    Guid Id,
    string DisplayName,
    string? DnsName = null,
    string? Os = null,
    PlatformKind? Kind = null,
    string? IpAddress = null,
    string? Notes = null,
    HashSet<Guid>? TagIds = null
);

public record DeletePlatform(
    Guid Id
);
