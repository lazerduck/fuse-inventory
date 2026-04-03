using System.Security.Claims;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.API.Middleware;

namespace Fuse.API.CurrentUser;

public static class ClaimsPrincipalExtensions
{
    public static bool IsLoggedIn(this ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true;

    public static bool IsAdmin(this ClaimsPrincipal? principal) =>
        string.Equals(
            principal?.FindFirst(AuthenticationMiddleware.IsAdminClaimType)?.Value,
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

    public static Guid? GetPrincipalId(this ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static IReadOnlyList<Guid> GetRoleIds(this ClaimsPrincipal? principal)
    {
        if (principal is null)
            return Array.Empty<Guid>();

        return principal.FindAll(AuthenticationMiddleware.RoleIdClaimType)
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    public static bool IsUserAuth(this ClaimsPrincipal? principal) =>
        string.Equals(
            principal?.Identity?.AuthenticationType,
            AuthenticationMiddleware.UserAuthType,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsApiKeyAuth(this ClaimsPrincipal? principal) =>
        string.Equals(
            principal?.Identity?.AuthenticationType,
            AuthenticationMiddleware.ApiKeyAuthType,
            StringComparison.OrdinalIgnoreCase);
}
