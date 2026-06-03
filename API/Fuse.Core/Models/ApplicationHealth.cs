namespace Fuse.Core.Models;

public record ApplicationHealth
(
    Guid ApplicationId,
    bool VersionSet,
    bool DescriptionSet,
    bool OwnerSet,
    bool FrameworkSet,
    int TagCount,
    int PipelineCount,
    List<ApplicationInstanceHealth> InstanceHealths
);

public record ApplicationInstanceHealth
(
    Guid InstanceId,
    bool PlatformSet,
    bool BaseUriSet,
    bool HealthUriSet,
    bool OpenApiUriSet,
    bool VersionSet,
    int DependencyCount,
    int TagCount
);