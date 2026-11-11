using Fuse.Core.Responses;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Entra;

public static class RoleMembershipResolver
{
    // Group grants are evaluated from a validated, unexpired session, never copied into direct assignments.
    public static IReadOnlyList<RoleMembership> Resolve(SecurityContext security, FuseUser user,
        IReadOnlyList<Guid>? verifiedGroups = null)
    {
        if (user.IsDisabled)
            return [];
        var groups = security.Entra.Enabled ? (verifiedGroups ?? []).ToHashSet() : [];
        return security.Roles.Select(role => new RoleMembership(role.Id, role.Name,
                user.RoleIds.Contains(role.Id), role.EntraGroups.Where(g => groups.Contains(g.GroupId)).ToArray()))
            .Where(m => m.Direct || m.EntraGroups.Count > 0).ToArray();
    }

    public static IReadOnlyList<Guid> RecentGroups(SecurityContext security, FuseUser user, DateTime now) =>
        security.Entra.Enabled && user.EntraTenantId == security.Entra.TenantId && user.EntraValidUntil > now
            ? user.LastEntraGroupIds : [];
}
