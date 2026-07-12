using Fuse.Core.Helpers;
using ModelContextProtocol;

namespace Fuse.MCP;

internal static class McpResult
{
    public static T Value<T>(Result<T> result, string fallback = "The inventory operation failed.") =>
        result.IsSuccess ? result.Value! : throw new McpException(result.Error ?? fallback);

    public static object Done(Result result, string fallback = "The inventory operation failed.")
    {
        if (!result.IsSuccess) throw new McpException(result.Error ?? fallback);
        return new { Success = true };
    }
}
