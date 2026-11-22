using Fuse.Core.Configs;
using Fuse.Core.Areas.Logging;
using Fuse.Core.Interfaces;
using Fuse.Core.Areas.Activity;
using Fuse.Core.Areas.Audit;
using Fuse.Core.Areas.PlatformAgent;
using Fuse.Data.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Fuse.Data;

public static class FuseDataModule
{
    public static void Register(IServiceCollection services)
    {
        var dataDirectory = Environment.GetEnvironmentVariable("FUSE_DATA_DIR") 
            ?? Path.Combine(AppContext.BaseDirectory, "data");
        
        services.AddSingleton(new JsonFuseStoreOptions { DataDirectory = dataDirectory });
        services.AddSingleton<IFuseStore, JsonFuseStore>();
        
        services.AddSingleton<IAuditService>(provider =>
            new LiteDbAuditService(provider.GetRequiredService<IFuseStore>(), dataDirectory));

        services.AddSingleton<ILogService>(provider =>
            new LiteDbLogService(provider.GetRequiredService<IFuseStore>(), dataDirectory));
        
        services.AddSingleton<IVersionHistoryService>(provider =>
            new LiteDbVersionHistoryService(provider.GetRequiredService<IFuseStore>(), dataDirectory));

        services.AddSingleton<IHealthMonitoringStore>(_ => new LiteDbHealthMonitoringStore(dataDirectory));
        services.AddSingleton<IAgentReportingStore>(provider => new LiteDbAgentReportingStore(
            provider.GetRequiredService<IFuseStore>(),
            provider.GetRequiredService<JsonFuseStoreOptions>().DataDirectory, TimeProvider.System));
    }
}
