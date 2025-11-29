namespace Fuse.Core.Models;

public record Application
(
    Guid Id,
    string Name,
    string? Version,
    string? Description,
    string? Owner,
    string? Notes,
    string? Framework,
    Uri? RepositoryUri,
    HashSet<Guid> TagIds,
    IReadOnlyList<ApplicationInstance> Instances,
    IReadOnlyList<ApplicationPipeline> Pipelines,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ApplicationPipeline
(
    Guid Id,
    string Name,
    Uri? PipelineUri
);

public record ApplicationInstance
(
    Guid Id,
    Guid EnvironmentId,
    Guid? PlatformId,
    Uri? BaseUri,
    Uri? HealthUri,
    Uri? OpenApiUri,
    string? Version,
    IReadOnlyList<ApplicationInstanceDependency> Dependencies,
    HashSet<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ApplicationInstanceDependency
(
    Guid Id,
    Guid TargetId,
    TargetKind TargetKind,
    int? Port,
    DependencyAuthKind AuthKind,
    Guid? AccountId,
    Guid? IdentityId
);
