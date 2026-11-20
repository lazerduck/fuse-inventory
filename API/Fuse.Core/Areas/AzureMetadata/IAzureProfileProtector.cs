namespace Fuse.Core.Areas.AzureMetadata;

public interface IAzureProfileProtector
{
    string Protect(Guid id, string value);
    string Unprotect(Guid id, string value);
}
