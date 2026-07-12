using Fuse.Core.Interfaces;

namespace Fuse.API.Mcp;

public sealed class McpAvailabilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IFuseStore store)
    {
        var enabled = await store.GetAsync(s => s.AppSettings.McpServerEnabled, context.RequestAborted);
        if (!enabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    }
}
