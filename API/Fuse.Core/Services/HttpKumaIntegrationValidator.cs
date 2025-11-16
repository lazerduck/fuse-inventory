using System.Net.Http.Headers;
using Fuse.Core.Interfaces;

namespace Fuse.Core.Services;

public class HttpKumaIntegrationValidator : IKumaIntegrationValidator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private const string TestEndpoint = "/api/status"; // typical uptime-kuma endpoint

    public HttpKumaIntegrationValidator(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> ValidateAsync(Uri baseUri, string apiKey, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return false;
            if (!baseUri.IsAbsoluteUri) return false;

            var client = _httpClientFactory.CreateClient("kuma-validator");
            client.BaseAddress = baseUri;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.Timeout = TimeSpan.FromSeconds(5);

            using var resp = await client.GetAsync(TestEndpoint, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
