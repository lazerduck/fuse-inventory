using System.Text.Json;
using System.Text.Json.Serialization;
using Fuse.API.CurrentUser;
using Fuse.API.Middleware;
using Fuse.Core;
using Fuse.Core.Interfaces;
using Fuse.Data;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "x-api-key",
        Type = SecuritySchemeType.ApiKey,
        Description = "API key authentication using the x-api-key header"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Bearer token authentication"
    });
    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("ApiKey", doc), new List<string>() },
        { new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>() }
    });
});

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
    var appInitializationService = scope.ServiceProvider.GetRequiredService<IAppInitializationService>();
    await appInitializationService.InitializeAsync();
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

// Explicit routing so that SecurityMiddleware can read endpoint metadata (e.g. [RequirePermission])
app.UseRouting();

// Apply security only to API routes so SPA static files and fallback aren't blocked
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), branch =>
{
    branch.UseMiddleware<AuthenticationMiddleware>();
    branch.UseMiddleware<AuthorizationMiddleware>();
});

app.MapControllers();

// Fallback to index.html for SPA routing (only in production)
if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.Run();
