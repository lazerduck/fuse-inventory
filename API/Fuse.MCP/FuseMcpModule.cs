using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;

namespace Fuse.MCP;

public static class FuseMcpModule
{
    public static IServiceCollection AddFuseMcp(this IServiceCollection services)
    {
        services.AddScoped<McpToolAuthorization>();
        services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithRequestFilters(filters =>
            {
                filters.AddListToolsFilter(next => async (context, cancellationToken) =>
                {
                    var result = await next(context, cancellationToken);
                    var http = context.Services?.GetService<IHttpContextAccessor>()?.HttpContext;
                    var profile = http?.Request.Query["profile"].FirstOrDefault();
                    result.Tools = result.Tools.Where(tool => McpToolProfiles.Includes(profile, tool)).ToList();
                    return result;
                });
            })
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
