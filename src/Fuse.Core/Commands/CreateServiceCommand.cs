using Fuse.Core.Enums;
using Fuse.Core.Manifests;

namespace Fuse.Core.Commands;

public record CreateServiceCommand(
    string Name,
    string? Version,
    string? Description,
    string? Notes,
    string? Author,
    string? Framework,
    ServiceType Type,
    Uri? RepositoryUri,
    List<DeploymentPipeline> DeploymentPipelines,
    List<Deployments> Deployments,
    List<Tags> Tags
);