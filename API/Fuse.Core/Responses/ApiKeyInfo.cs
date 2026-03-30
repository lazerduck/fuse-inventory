using System;
using System.Collections.Generic;

namespace Fuse.Core.Responses;

public record ApiKeyInfo(
    Guid Id,
    string Name,
    Guid UserId,
    IReadOnlyList<Guid> RoleIds,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
