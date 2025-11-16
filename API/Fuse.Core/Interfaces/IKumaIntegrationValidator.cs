namespace Fuse.Core.Interfaces;

public interface IKumaIntegrationValidator
{
    Task<bool> ValidateAsync(Uri baseUri, string apiKey, CancellationToken ct = default);
}
