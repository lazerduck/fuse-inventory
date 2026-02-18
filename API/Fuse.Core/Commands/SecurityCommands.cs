using System;
using System.Collections.Generic;
using Fuse.Core.Models;

namespace Fuse.Core.Commands;

public record UpdateSecuritySettings(SecurityLevel Level)
{
    public Guid? RequestedBy { get; init; }
}

public record CreateSecurityUser(string UserName, string Password, SecurityRole? Role = null)
{
    public Guid? RequestedBy { get; init; }
}

public record LoginSecurityUser(string UserName, string Password);

public record LogoutSecurityUser(string Token);

public record DeleteUser(Guid Id)
{
    public Guid? RequestedBy { get; init; }
}

public record UpdateUser(Guid Id, SecurityRole Role)
{
    public Guid? RequestedBy { get; init; }
}

// Role management commands
public record CreateRole(string Name, string Description, IReadOnlyList<Permission> Permissions)
{
    public Guid? RequestedBy { get; init; }
}

public record UpdateRole(Guid Id, string Name, string Description, IReadOnlyList<Permission> Permissions)
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