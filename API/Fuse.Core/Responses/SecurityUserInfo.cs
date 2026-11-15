using System;
using System.Collections.Generic;
using Fuse.Core.Models;
using Fuse.Core.Areas.Security.Entra;

namespace Fuse.Core.Responses;

public record SecurityUserInfo(
    Guid Id,
    string UserName,
    bool IsAdmin,
    IReadOnlyList<Guid>? RoleIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public bool IsDisabled { get; init; }
    public bool HasLocalPassword { get; init; }
    public Guid? EntraObjectId { get; init; }
    public DateTime? EntraVerifiedAt { get; init; }
    public DateTime? EntraValidUntil { get; init; }
    public IReadOnlyList<RoleMembership> RoleMemberships { get; init; } = [];
    public IReadOnlyList<Guid> EffectiveRoleIds { get; init; } = [];

    public SecurityUserInfo(FuseUser user)
        : this(user.Id, user.UserName, user.IsAdmin, user.RoleIds, user.CreatedAt, user.UpdatedAt)
    {
        IsDisabled = user.IsDisabled;
        HasLocalPassword = !string.IsNullOrEmpty(user.PasswordHash);
        EntraObjectId = user.EntraObjectId;
        EntraVerifiedAt = user.EntraVerifiedAt;
        EntraValidUntil = user.EntraValidUntil;
    }

    public static SecurityUserInfo FromFuseUser(FuseUser user)
    {
        return new SecurityUserInfo(user);
    }

}
