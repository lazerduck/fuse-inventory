using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fuse.MCP;

public static class FuseMcpModule
{
    public static IServiceCollection AddFuseMcp(this IServiceCollection services)
    {
        services.AddScoped<McpToolAuthorization>();
        services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<ApplicationTools>()
            .WithTools<InventoryReadTools>()
            .WithTools<AccountTools>()
            .WithTools<InfrastructureTools>()
            .WithTools<GovernanceTools>()
            .WithTools<PatchTools>();
        return services;
    }

    public static IEndpointConventionBuilder MapFuseMcp(this IEndpointRouteBuilder endpoints, string route = "/api/mcp") =>
        endpoints.MapMcp(route);
}
