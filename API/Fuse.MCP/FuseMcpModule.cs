using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Fuse.MCP;

public static class FuseMcpModule
{
    public static IServiceCollection AddFuseMcp(this IServiceCollection services)
    {
        services.AddScoped<McpToolAuthorization>();
        services.AddAuthorization();
        services.AddSingleton<IConfigureOptions<AuthorizationOptions>, McpAuthorizationPolicies>();
        services.AddScoped<IAuthorizationHandler, McpPermissionHandler>();
        services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .AddAuthorizationFilters()
            .WithTools<ApplicationTools>()
            .WithTools<InventoryReadTools>()
            .WithTools<AccountTools>()
            .WithTools<InfrastructureTools>()
            .WithTools<GovernanceTools>()
            .WithTools<PatchTools>()
            .WithTools<SecretTools>();
        return services;
    }

    public static IEndpointConventionBuilder MapFuseMcp(this IEndpointRouteBuilder endpoints, string route = "/api/mcp") =>
        endpoints.MapMcp(route);
}
