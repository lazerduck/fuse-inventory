namespace Fuse.Core.Models;

public record SecurityContext(
    SecurityPosture Posture,
    IReadOnlyList<FuseRole> Roles,
    IReadOnlyList<FuseUser> Users,
    IReadOnlyList<FuseApiKey> ApiKeys,
    IReadOnlyList<Session> Sessions
)
{
    public EntraSettings Entra { get; init; } = new();

    public IReadOnlyDictionary<Guid, UserGuideProgress> GuideProgress { get; init; }
        = new Dictionary<Guid, UserGuideProgress>();
}

public record UserGuideProgress(
    IReadOnlyList<string> CompletedStepIds,
    string? ActiveGuideId,
    bool HasCompletedGettingStarted,
    DateTime? LastCompletedAt,
    DateTime UpdatedAt,
    Guid? ApplicationId = null);

public record FuseRole(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public IReadOnlyList<EntraGroupMapping> EntraGroups { get; init; } = [];
}

public record FuseUser(
    Guid Id,
    string UserName,
    string PasswordHash,
    string PasswordSalt,
    bool IsAdmin,
    IReadOnlyList<Guid> RoleIds,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public bool IsDisabled { get; init; }
    public int SessionVersion { get; init; }
    public Guid? EntraTenantId { get; init; }
    public Guid? EntraObjectId { get; init; }
    public IReadOnlyList<Guid> LastEntraGroupIds { get; init; } = [];
    public DateTime? EntraVerifiedAt { get; init; }
    public DateTime? EntraValidUntil { get; init; }
}

public record FuseApiKey(
    Guid Id,
    string Name,
    string KeyPrefix,
    string KeyHash,
    string KeySalt,
    Guid UserId,
    IReadOnlyList<Guid> RoleIds,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record Session(
    string Token, 
    Guid UserId, 
    DateTime ExpiresAt)
{
    public int Version { get; init; }
    public bool IsEntra { get; init; }
    public Guid? EntraConfigurationVersion { get; init; }
    public IReadOnlyList<Guid> EntraGroupIds { get; init; } = [];
}

public enum SecurityPosture
{
    Unrestricted,
    RestrictedEditing,
    FullyRestricted
}

public record EntraGroupMapping(Guid GroupId, string DisplayName);

public record EntraSettings
{
    public bool Enabled { get; init; }
    public Guid TenantId { get; init; }
    public Guid ClientId { get; init; }
    public string ProtectedClientSecret { get; init; } = "";
    public string PublicOrigin { get; init; } = "";
    public int SessionMinutes { get; init; } = 60;
    public Guid Version { get; init; } = Guid.NewGuid();
}
