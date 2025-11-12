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
        services.AddScoped<IKumaIntegrationValidator, HttpKumaIntegrationValidator>();
    }
}