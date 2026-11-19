using Fuse.Core.Areas.Security;
using Fuse.Core.Models;
using System.Security.Claims;

namespace Fuse.API.CurrentUser;

// For actions that select a provider or combine inventory records after routing.
public sealed class RequestPermissions(IEnumerable<AreaPermissions> catalogs)
{
    private readonly Dictionary<string, PermissionDescriptor> descriptors = catalogs.SelectMany(c => c.GetPermissionDescriptors())
        .ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    public bool Allows(ClaimsPrincipal user, Snapshot snapshot, string key)
    {
        if (!descriptors.TryGetValue(key, out var descriptor)) return false;
        if (user.IsAdmin()) return true;
        var security = snapshot.SecurityContext;
        if (!descriptor.IgnorePosture && (security.Posture == SecurityPosture.Unrestricted ||
            security.Posture == SecurityPosture.RestrictedEditing && descriptor.IsAllowedInRestrictedEditing)) return true;
        if (!user.IsLoggedIn()) return false;
        var roles = user.GetRoleIds();
        return security.Roles.Any(r => roles.Contains(r.Id) && r.Permissions.Contains(key, StringComparer.OrdinalIgnoreCase));
    }
}
