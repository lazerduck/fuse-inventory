namespace Fuse.Core.Models;

public record SecurityContext(
    SecurityPosture Posture,
    IReadOnlyList<FuseRole> Roles,
    IReadOnlyList<FuseUser> Users,
    IReadOnlyList<FuseApiKey> ApiKeys,
    IReadOnlyList<Session> Sessions
)
{
    public IReadOnlyDictionary<Guid, UserGuideProgress> GuideProgress { get; init; }
        = new Dictionary<Guid, UserGuideProgress>();
}

public record UserGuideProgress(
    IReadOnlyList<string> CompletedStepIds,
    string? ActiveGuideId,
    bool HasCompletedGettingStarted,
    DateTime? LastCompletedAt,
    DateTime UpdatedAt);

public record FuseRole(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record FuseUser(
    Guid Id,
    string UserName,
    string PasswordHash,
    string PasswordSalt,
    bool IsAdmin,
    IReadOnlyList<Guid> RoleIds,
    DateTime CreatedAt,
    DateTime UpdatedAt);

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
    DateTime ExpiresAt);

public enum SecurityPosture
{
    Unrestricted,
    RestrictedEditing,
    FullyRestricted
}
