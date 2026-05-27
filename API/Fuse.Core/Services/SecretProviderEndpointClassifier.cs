namespace Fuse.Core.Services;

public static class SecretProviderEndpointClassifier
{
    public static bool IsAppConfigurationEndpoint(Uri endpoint)
        => endpoint.Host.EndsWith(".azconfig.io", StringComparison.OrdinalIgnoreCase);
}
