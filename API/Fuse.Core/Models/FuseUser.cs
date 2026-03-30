namespace Fuse.Core.Models;

public record SecurityContext(
    SecurityPosture Posture,
    IReadOnlyList<FuseRole> Roles,
    IReadOnlyList<FuseUser> Users,
    IReadOnlyList<FuseApiKey> ApiKeys,
    IReadOnlyList<Session> Sessions
);

public record FuseRole(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<string>
    Permissions,
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