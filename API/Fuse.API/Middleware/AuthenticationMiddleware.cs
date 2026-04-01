using System.Security.Claims;
using Fuse.Core.Areas.Security.Interfaces;

namespace Fuse.API.Middleware;

public sealed class AuthenticationMiddleware
{
    public const string RoleIdClaimType = "role_id";
    public const string IsAdminClaimType = "is_admin";
    public const string ApiKeyAuthType = "ApiKey";
    public const string UserAuthType = "User";

    private readonly RequestDelegate _next;

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAPIKeyService apiKeyService, IFuseUserSessionService sessionService, IFuseUserService userService)
    {
        var cancellationToken = context.RequestAborted;

        // API key takes priority when both headers are present
        var rawApiKey = ExtractApiKey(context.Request);
        if (rawApiKey is not null)
        {
            var principal = await TryAuthenticateApiKeyAsync(rawApiKey, apiKeyService, cancellationToken);
            if (principal is not null)
                context.User = principal;
        }
        else
        {
            var token = ExtractBearerToken(context.Request);
            if (token is not null)
            {
                var principal = await TryAuthenticateTokenAsync(token, sessionService, userService, cancellationToken);
                if (principal is not null)
                    context.User = principal;
            }
        }

        await _next(context);
    }

    private static async Task<ClaimsPrincipal?> TryAuthenticateApiKeyAsync(
        string rawKey,
        IAPIKeyService apiKeyService,
        CancellationToken cancellationToken)
    {
        var result = await apiKeyService.VerifyAPIKeys(rawKey);
        if (!result.IsSuccess)
            return null;

        var key = result.Value!;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, key.Name),
            new(ClaimTypes.NameIdentifier, key.Id.ToString()),
        };

        foreach (var roleId in key.RoleIds)
            claims.Add(new Claim(RoleIdClaimType, roleId.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, ApiKeyAuthType));
    }

    private static async Task<ClaimsPrincipal?> TryAuthenticateTokenAsync(
        string token,
        IFuseUserSessionService sessionService,
        IFuseUserService userService,
        CancellationToken cancellationToken)
    {
        var sessionResult = await sessionService.ValidateSession(token);
        if (!sessionResult.IsSuccess)
            return null;

        var userResult = await userService.GetUser(sessionResult.Value!);
        if (!userResult.IsSuccess)
            return null;

        var user = userResult.Value!;

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };

        if (user.IsAdmin)
            claims.Add(new Claim(IsAdminClaimType, bool.TrueString));

        foreach (var roleId in user.RoleIds)
            claims.Add(new Claim(RoleIdClaimType, roleId.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, UserAuthType));
    }

    private static string? ExtractBearerToken(HttpRequest request)
    {
        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (authHeader is null || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authHeader["Bearer ".Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }

    private static string? ExtractApiKey(HttpRequest request)
    {
        var value = request.Headers["x-api-key"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
