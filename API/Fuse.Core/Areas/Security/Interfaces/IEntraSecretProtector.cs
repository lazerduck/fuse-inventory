namespace Fuse.Core.Areas.Security.Interfaces;

public interface IEntraSecretProtector
{
    string Protect(string secret);
}
