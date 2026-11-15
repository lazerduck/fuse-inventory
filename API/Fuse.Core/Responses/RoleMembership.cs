using Fuse.Core.Models;

namespace Fuse.Core.Responses;

public record RoleMembership(Guid RoleId, string RoleName, bool Direct, IReadOnlyList<EntraGroupMapping> EntraGroups);
