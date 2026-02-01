using System.Text.Json.Serialization;
using Fuse.API.CurrentUser;
using Fuse.API.Middleware;
using Fuse.Core;
using Fuse.Core.Interfaces;
using Fuse.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowViteDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

FuseDataModule.Register(builder.Services);
FuseCodeModule.Register(builder.Services);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<IFuseStore>();
    await store.LoadAsync();
    
    // Initialize default roles if they don't exist
    var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
    var roles = await permissionService.EnsureDefaultRolesAsync();
    
    // Update the store with default roles if needed
    var state = await store.GetAsync();
    if (!state.Security.Roles.Any())
    {
        await store.UpdateAsync(s => s with
        {
            Security = s.Security with { Roles = roles.ToList() }
        });
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("AllowViteDev");
}
else
{
    // Serve static files from the Vue build output
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseHttpsRedirection();

// Apply security only to API routes so SPA static files and fallback aren't blocked
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), branch =>
{
    branch.UseMiddleware<SecurityMiddleware>();
});

app.MapControllers();

// Fallback to index.html for SPA routing (only in production)
if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
