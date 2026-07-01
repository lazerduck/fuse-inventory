namespace Fuse.Core.Areas.KumaIntegration;

public interface IKumaIntegrationValidator
{
    Task<bool> ValidateAsync(Uri baseUri, string apiKey, CancellationToken ct = default);
}
