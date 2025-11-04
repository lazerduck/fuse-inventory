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
        services.AddScoped<IServerService, ServerService>();
        services.AddScoped<IDataStoreService, DataStoreService>();
        services.AddScoped<IExternalResourceService, ExternalResourceService>();
    }
}