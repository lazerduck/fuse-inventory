using Fuse.Core.Configs;
using Fuse.Core.Interfaces;
using Fuse.Data.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Fuse.Data;

public static class FuseDataModule
{
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<IFuseStore>(_ =>
            new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = Path.Combine(AppContext.BaseDirectory, "data") }));
    }
}