using Fuse.Core.Areas.Security.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Fuse.API.Entra;

public sealed class EntraSecretProtector(IDataProtectionProvider protection) : IEntraSecretProtector
{
    public string Protect(string secret)
    {
        return protection.CreateProtector(EntraAuthentication.SecretPurpose).Protect(secret);
    }
}
