using Fuse.Core;
using Fuse.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Services.AddSpaStaticFiles(opt => opt.RootPath = "Fuse.Web/dist");

FuseCoreModule.RegisterServices(builder.Services);
FuseDataModule.RegisterServices(builder.Services);

var app = builder.Build();

app.UseRouting();

app.UseSpaStaticFiles();

// Map API controllers BEFORE SPA
app.MapControllers();

// Use MapWhen to only apply SPA proxy to non-API routes
app.MapWhen(context => !context.Request.Path.StartsWithSegments("/api"), spa =>
{
    spa.UseSpa(spaBuilder =>
    {
        spaBuilder.Options.SourcePath = "Web";

        if (app.Environment.IsDevelopment())
        {
            spaBuilder.UseProxyToSpaDevelopmentServer("http://localhost:5173");
        }
    });
});

app.Run();
