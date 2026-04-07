using System;
using System.Collections.Generic;
using Fuse.Core.Models;

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
    public SecurityUserInfo(FuseUser user)
        : this(user.Id, user.UserName, user.IsAdmin, user.RoleIds, user.CreatedAt, user.UpdatedAt)
    {
    }

    public SecurityUserInfo(SecurityUser user)
        : this(
            user.Id,
            user.UserName,
            user.Role == SecurityRole.Admin,
            user.RoleIds,
            user.CreatedAt,
            user.UpdatedAt)
    {
    }

    public static SecurityUserInfo FromFuseUser(FuseUser user)
    {
        return new SecurityUserInfo(user);
    }

    public static SecurityUserInfo FromSecurityUser(SecurityUser user)
    {
        return new SecurityUserInfo(user);
    }
}

