using Fuse.Core.Models;

namespace Fuse.Core.Commands;

// Applications
public record CreateApplication(
    string Name,
    string? Version,
    string? Description,
    string? Owner,
    string? Notes,
    string? Framework,
    Uri? RepositoryUri,
    string? Icon = null,
    HashSet<Guid>? TagIds = null
);

public record UpdateApplication(
    Guid Id,
    string Name,
    string? Version,
    string? Description,
    string? Owner,
    string? Notes,
    string? Framework,
    Uri? RepositoryUri,
    string? Icon = null,
    HashSet<Guid>? TagIds = null
);

public record DeleteApplication(
    Guid Id
);

// Instances
public record CreateApplicationInstance(
    Guid ApplicationId,
    Guid EnvironmentId,
    Guid? PlatformId,
    Uri? BaseUri,
    Uri? HealthUri,
    Uri? OpenApiUri,
    string? Version,
    HashSet<Guid>? TagIds = null
);

public record UpdateApplicationInstance(
    Guid ApplicationId,
    Guid InstanceId,
    Guid EnvironmentId,
    Guid? PlatformId,
    Uri? BaseUri,
    Uri? HealthUri,
    Uri? OpenApiUri,
    string? Version,
    HashSet<Guid>? TagIds = null
);

public record DeleteApplicationInstance(
    Guid ApplicationId,
    Guid InstanceId
);

// Pipelines
public record CreateApplicationPipeline(
    Guid ApplicationId,
    string Name,
    Uri? PipelineUri
);

public record UpdateApplicationPipeline(
    Guid ApplicationId,
    Guid PipelineId,
    string Name,
    Uri? PipelineUri
);

public record DeleteApplicationPipeline(
    Guid ApplicationId,
    Guid PipelineId
);

// Dependencies
public record CreateApplicationDependency(
    Guid ApplicationId,
    Guid InstanceId,
    Guid TargetId,
    TargetKind TargetKind,
    int? Port,
    DependencyAuthKind AuthKind,
    Guid? AccountId,
    Guid? IdentityId
);

public record UpdateApplicationDependency(
    Guid ApplicationId,
    Guid InstanceId,
    Guid DependencyId,
    Guid TargetId,
    TargetKind TargetKind,
    int? Port,
    DependencyAuthKind AuthKind,
    Guid? AccountId,
    Guid? IdentityId
);

public record DeleteApplicationDependency(
    Guid ApplicationId,
    Guid InstanceId,
    Guid DependencyId
);
