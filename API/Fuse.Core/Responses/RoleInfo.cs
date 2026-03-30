using System;
using System.Collections.Generic;
using Fuse.Core.Models;

namespace Fuse.Core.Responses;

public record RoleInfo(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<Permission> Permissions,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
