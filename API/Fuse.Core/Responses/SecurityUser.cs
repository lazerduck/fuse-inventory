using Fuse.Core.Models;

namespace Fuse.Core.Responses;

public record SecurityUserResponse (
  Guid Id,
  string UserName,
  SecurityRole Role,
  DateTime CreatedAt,
  DateTime UpdatedAt
);