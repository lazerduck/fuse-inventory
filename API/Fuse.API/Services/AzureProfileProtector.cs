using Fuse.Core.Areas.AzureMetadata;
using Microsoft.AspNetCore.DataProtection;

namespace Fuse.API.Services;

public sealed class AzureProfileProtector(IDataProtectionProvider provider) : IAzureProfileProtector
{
    private IDataProtector For(Guid id) => provider.CreateProtector("Fuse.Azure.CredentialProfile.v1", id.ToString("D"));
    public string Protect(Guid id, string value) => For(id).Protect(value);
    public string Unprotect(Guid id, string value) => For(id).Unprotect(value);
}
