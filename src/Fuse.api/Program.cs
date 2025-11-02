using Fuse.Core;
using Fuse.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
    });

builder.Services.AddSpaStaticFiles(opt => opt.RootPath = "Fuse.Web/dist");

FuseCoreModule.RegisterServices(builder.Services);
FuseDataModule.RegisterServices(builder.Services);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SchemaGeneratorOptions.UseAllOfToExtendReferenceSchemas = false;
    c.UseInlineDefinitionsForEnums();
});

builder.Services.AddSwaggerGenNewtonsoftSupport();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

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
