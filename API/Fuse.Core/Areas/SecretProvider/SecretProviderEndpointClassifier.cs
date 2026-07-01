namespace Fuse.Core.Areas.SecretProvider;

public static class SecretProviderEndpointClassifier
{
    public static bool IsAppConfigurationEndpoint(Uri endpoint)
        => endpoint.Host.EndsWith(".azconfig.io", StringComparison.OrdinalIgnoreCase);
}
