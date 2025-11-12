using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record CreateKumaIntegration(
    string? Name,
    IReadOnlyList<Guid> EnvironmentIds,
    Guid? PlatformId,
    Guid? AccountId,
    Uri Uri,
    string ApiKey
);

public record UpdateKumaIntegration(
    Guid Id,
    string? Name,
    IReadOnlyList<Guid> EnvironmentIds,
    Guid? PlatformId,
    Guid? AccountId,
    Uri Uri,
    string ApiKey
);

public record DeleteKumaIntegration(Guid Id);
