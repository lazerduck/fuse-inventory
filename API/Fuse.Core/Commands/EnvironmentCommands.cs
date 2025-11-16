 

namespace Fuse.Core.Commands;

public record CreateEnvironment (
    string Name,
    string? Description,
    HashSet<Guid>? TagIds = null,
    bool AutoCreateInstances = false,
    string? BaseUriTemplate = null,
    string? HealthUriTemplate = null,
    string? OpenApiUriTemplate = null
);

public record UpdateEnvironment (
    Guid Id,
    string Name,
    string? Description,
    HashSet<Guid>? TagIds = null,
    bool AutoCreateInstances = false,
    string? BaseUriTemplate = null,
    string? HealthUriTemplate = null,
    string? OpenApiUriTemplate = null
);

public record DeleteEnvironment (
    Guid Id
    //ToDo - Extend with deletion type (Cascase or Reassign)
);

public record ApplyEnvironmentAutomation (
    Guid? EnvironmentId = null,
    Guid? ApplicationId = null
);
