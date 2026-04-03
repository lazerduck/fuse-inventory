using System;
using System.Collections.Generic;
using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record UpdateSecuritySettings(SecurityPosture Posture)
{
    public Guid? RequestedBy { get; init; }
}

public record CreateSecurityUser(string UserName, string Password, List<Guid> RoleIds, bool IsAdmin = false);

public record LoginSecurityUser(string UserName, string Password);

public record LogoutSecurityUser(string Token);

public record DeleteUser(Guid Id)
{
    public Guid? RequestedBy { get; init; }
}

// Role management commands
public record CreateRole(string Name, string Description, IReadOnlyList<string> Permissions)
{
    public Guid? RequestedBy { get; init; }
}

public record UpdateRole(Guid Id, string Name, string Description, IReadOnlyList<string> Permissions)
{
    public Guid? RequestedBy { get; init; }
}

public record DeleteRole(Guid Id)
{
    public Guid? RequestedBy { get; init; }
}

public record AssignRolesToUser(Guid UserId, IReadOnlyList<Guid> RoleIds)
{
    public Guid? RequestedBy { get; init; }
}

public record ResetPassword(Guid TargetUserId, string NewPassword)
{
    public Guid? RequestedBy { get; init; }
    public string? CurrentPassword { get; init; }
}

public record CreateApiKey(string Name, IReadOnlyList<Guid> RoleIds)
{
    public Guid? RequestedBy { get; init; }
}

public record RegenerateApiKey(Guid Id)
{
    public Guid? RequestedBy { get; init; }
}

public record DeleteApiKey(Guid Id)
{
    public Guid? RequestedBy { get; init; }
}