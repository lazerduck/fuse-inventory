using Fuse.Core.Interfaces;
using Fuse.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fuse.Core;

public static class FuseCodeModule
{
    public static void Register(IServiceCollection services)
    {
        services.AddScoped<IEnvironmentService, EnvironmentService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<IDataStoreService, DataStoreService>();
        services.AddScoped<IExternalResourceService, ExternalResourceService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IConfigService, ConfigService>();
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddScoped<IKumaIntegrationService, KumaIntegrationService>();
        services.AddHttpClient("kuma-validator");
        services.AddHttpClient("kuma-metrics");
        services.AddScoped<IKumaIntegrationValidator, HttpKumaIntegrationValidator>();
        
        // Register KumaMetricsService as both hosted service and singleton for health queries
        services.AddSingleton<KumaMetricsService>();
        services.AddHostedService(provider => provider.GetRequiredService<KumaMetricsService>());
        services.AddSingleton<IKumaHealthService>(provider => provider.GetRequiredService<KumaMetricsService>());
        
        // Register Secret Provider services
        services.AddScoped<ISecretProviderService, SecretProviderService>();
        services.AddScoped<IAzureKeyVaultClient, AzureKeyVaultClient>();
        services.AddScoped<ISecretOperationService, SecretOperationService>();
        
        // Register SQL Integration services
        services.AddScoped<ISqlIntegrationService, SqlIntegrationService>();
        services.AddScoped<ISqlConnectionValidator, SqlConnectionValidator>();
    }
}