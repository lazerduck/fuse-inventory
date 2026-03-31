using System.Security.Cryptography;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Security.Services;

public class Authentication(IAPIKeyService apiKeyService) : IAuthenticationService
{
    public Result<IReadOnlyList<FuseRole>> GetAPIKeyRoles(string key)
    {
        throw new NotImplementedException();
    }

    public Result<IReadOnlyList<FuseRole>> GetUserRoles(string Token)
    {
        throw new NotImplementedException();
    }

    private static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var computed = HashPassword(password, storedSalt);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(storedHash),
            Convert.FromBase64String(computed));
    }

    private static string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            100_000,
            HashAlgorithmName.SHA256,
            32);
        return Convert.ToBase64String(derived);
    }
}