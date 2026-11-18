using Fuse.Core.Helpers;

namespace Fuse.Core.Areas.RedisIntegration;

public static class RedisConnectionSettings
{
    public static Result Validate(Uri? endpoint, string? username, string? password)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme is not ("redis" or "rediss")
            || string.IsNullOrWhiteSpace(endpoint.Host) || endpoint.Port == 0
            || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment) || endpoint.AbsolutePath is not ("" or "/"))
            return Result.Failure("Use a redis:// or rediss:// server endpoint with an optional port, without credentials, database path, query or fragment.");
        if (username is { Length: > 256 } || username?.Any(char.IsControl) == true
            || username is { Length: > 0 } && string.IsNullOrWhiteSpace(username))
            return Result.Failure("Redis usernames must contain at most 256 characters, no control characters and cannot consist only of whitespace.");
        if (password is { Length: > 4096 })
            return Result.Failure("The Redis password must contain at most 4096 characters.");
        return Result.Success();
    }

    public static string Username(string? username) => string.IsNullOrEmpty(username) ? "default" : username;
}
