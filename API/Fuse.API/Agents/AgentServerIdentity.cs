using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Fuse.Core.Configs;

namespace Fuse.API.Agents;

/// <summary>A durable, application-local identity. Agents pin the public key during pairing.</summary>
public sealed class AgentServerIdentity : IDisposable
{
    public X509Certificate2 Certificate { get; }
    public string Pin { get; }

    public AgentServerIdentity(JsonFuseStoreOptions options)
    {
        var directory = Path.Combine(options.DataDirectory, "agent-identity");
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = Path.Combine(directory, "instance.pk8");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        if (!File.Exists(path)) SaveNewKey(path, key);
        var encoded = File.ReadAllBytes(path);
        try { key.ImportPkcs8PrivateKey(encoded, out _); }
        finally { CryptographicOperations.ZeroMemory(encoded); }
        Pin = Convert.ToBase64String(SHA256.HashData(key.ExportSubjectPublicKeyInfo()));
        var request = new CertificateRequest("CN=Fuse Agent Reporting", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        Certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    }

    private static void SaveNewKey(string path, ECDsa key)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        try
        {
            using (var file = new FileStream(temporary, options))
            {
                var encoded = key.ExportPkcs8PrivateKey();
                try { file.Write(encoded); file.Flush(true); }
                finally { CryptographicOperations.ZeroMemory(encoded); }
            }
            try { File.Move(temporary, path); }
            catch (IOException) when (File.Exists(path)) { /* A concurrent initialisation won. */ }
        }
        finally { File.Delete(temporary); }
    }

    public void Dispose() => Certificate.Dispose();
}
