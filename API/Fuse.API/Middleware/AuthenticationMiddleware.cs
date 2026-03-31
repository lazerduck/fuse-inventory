using System.Security.Claims;

namespace Fuse.API.Middleware;

public sealed class AuthenticationMiddleware
{
	private readonly RequestDelegate _next;

	public AuthenticationMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		var cancellationToken = context.RequestAborted;

		// TODO: Add authentication flow for bearer and API key credentials.
		var token = ExtractBearerToken(context.Request);
		var apiKey = ExtractApiKey(context.Request);

		// TODO: Replace placeholder with real principal resolution.
		var principal = await TryAuthenticateAsync(token, apiKey, cancellationToken);
		if (principal is not null)
		{
			context.User = principal;
		}

		await _next(context);
	}

	private static string? ExtractBearerToken(HttpRequest request)
	{
		// TODO: Parse and validate Authorization header format.
		return null;
	}

	private static string? ExtractApiKey(HttpRequest request)
	{
		// TODO: Read API key header and normalize value.
		return null;
	}

	private static Task<ClaimsPrincipal?> TryAuthenticateAsync(string? token, string? apiKey, CancellationToken cancellationToken)
	{
		// TODO: Validate credentials and build role-based claims principal.
		return Task.FromResult<ClaimsPrincipal?>(null);
	}
}
