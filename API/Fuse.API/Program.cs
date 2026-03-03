using System.Text.Json;
using System.Text.Json.Serialization;
using Fuse.API.CurrentUser;
using Fuse.API.Middleware;
using Fuse.Core;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
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
    var store = scope.ServiceProvider.GetRequiredService<IFuseStore>();
    await store.LoadAsync();
    
    // Initialize default roles if they don't exist
    var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
    var defaultRoles = await permissionService.EnsureDefaultRolesAsync();
    
    // Update the store with default roles if needed
    var state = await store.GetAsync();
    var existingRoleIds = state.Security.Roles.Select(r => r.Id).ToHashSet();
    var missingRoles = defaultRoles.Where(r => !existingRoleIds.Contains(r.Id)).ToList();
    
    if (missingRoles.Any())
    {
        var allRoles = state.Security.Roles.Concat(missingRoles).ToList();
        await store.UpdateAsync(s => s with
        {
            Security = s.Security with { Roles = allRoles }
        });
    }

    // Migrate any legacy users that have no built-in role in their RoleIds yet
    var currentState = await store.GetAsync();
    var usersNeedingMigration = currentState.Security.Users.Where(u =>
        (u.Role == SecurityRole.Admin && !u.RoleIds.Contains(BuiltInRoles.AdminRoleId)) ||
        (u.Role == SecurityRole.Reader && !u.RoleIds.Contains(BuiltInRoles.ReaderRoleId) && !u.RoleIds.Contains(BuiltInRoles.AdminRoleId))).ToList();

    if (usersNeedingMigration.Any())
    {
        await store.UpdateAsync(s => s with
        {
            Security = s.Security with
            {
                Users = s.Security.Users.Select(u =>
                {
                    if (u.Role == SecurityRole.Admin && !u.RoleIds.Contains(BuiltInRoles.AdminRoleId))
                        return u with { RoleIds = u.RoleIds.Append(BuiltInRoles.AdminRoleId).ToList() };
                    if (u.Role == SecurityRole.Reader && !u.RoleIds.Contains(BuiltInRoles.ReaderRoleId) && !u.RoleIds.Contains(BuiltInRoles.AdminRoleId))
                        return u with { RoleIds = u.RoleIds.Append(BuiltInRoles.ReaderRoleId).ToList() };
                    return u;
                }).ToList()
            }
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
