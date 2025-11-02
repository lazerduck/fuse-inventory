using Fuse.Core.Enums;

namespace Fuse.Core.Manifests
{
    public record ServiceManifest(
        Guid Id,
        string Name,
        string? Version,
        string? Description,
        string? Notes,
        string? Author,
        string? Framework,
        ServiceType Type,
        Uri? RepositoryUri,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        List<DeploymentPipeline> DeploymentPipelines,
        List<Deployments> Deployments,
        List<Tags> Tags
    );

    public record DeploymentPipeline(string Name, Uri? PipelineUri);

    public record Deployments(
        string EnvironmentName,
        Uri? DeploymentUri,
        Uri? SwaggerUri,
        Uri? HealthUri,
        ServiceDeploymentStatus Status
    );


}