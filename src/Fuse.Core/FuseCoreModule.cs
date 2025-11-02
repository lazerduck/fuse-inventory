using Fuse.Core.Interfaces;
using Fuse.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fuse.Core
{
    public class FuseCoreModule
    {
        public static void RegisterServices(IServiceCollection services)
        {
            services.AddScoped<IServiceService, ServiceService>();
        }
    }
}