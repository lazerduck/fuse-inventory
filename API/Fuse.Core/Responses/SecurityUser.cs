using System;
using System.Collections.Generic;
using Fuse.Core.Models;

namespace Fuse.Core.Responses;

public record SecurityUserResponse (
  Guid Id,
  string UserName,
  bool IsAdmin,
  IReadOnlyList<Guid> RoleIds,
  DateTime CreatedAt,
  DateTime UpdatedAt
);