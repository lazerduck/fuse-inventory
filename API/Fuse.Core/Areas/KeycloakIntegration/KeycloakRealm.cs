using System.Text.RegularExpressions;

namespace Fuse.Core.Areas.KeycloakIntegration;

internal static partial class KeycloakRealm
{
    // Accept a realm issuer (including an optional deployment prefix), never an admin or discovery URL.
    [GeneratedRegex(@"^/(?:[A-Za-z0-9._~-]+/)*realms/[A-Za-z0-9._~-]+/?$", RegexOptions.CultureInvariant)]
    private static partial Regex IssuerPath();

    internal static bool Valid(Uri? uri) => uri is { IsAbsoluteUri: true } && uri.Scheme is "http" or "https"
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 && IssuerPath().IsMatch(uri.AbsolutePath)
        && !uri.OriginalString.Contains('\\') && !uri.OriginalString.Contains('%')
        && !uri.OriginalString.Split('/').Any(segment => segment is "." or "..");

    internal static string Identity(Uri issuer) => issuer.AbsoluteUri.TrimEnd('/');
    internal static string AdminBase(Uri issuer)
    {
        var identity = Identity(issuer);
        var index = identity.LastIndexOf("/realms/", StringComparison.Ordinal);
        return identity.Insert(index, "/admin") + "/";
    }
}
