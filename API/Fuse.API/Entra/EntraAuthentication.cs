using System.Security.Claims;
using Fuse.Core.Helpers;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Security.Entra;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Fuse.API.Entra;

public static class EntraAuthentication
{
    public const string Scheme = "Entra";
    public const string HandoffCookie = "fuse-entra-handoff";
    public const string LinkCookie = "fuse-entra-link";
    public const string SecretPurpose = "Fuse.Entra.ClientSecret.v1";

    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<EntraFlowTickets>();
        services.AddSingleton<IEntraSecretProtector, EntraSecretProtector>();
        var dataDirectory = Environment.GetEnvironmentVariable("FUSE_DATA_DIR") ?? Path.Combine(AppContext.BaseDirectory, "data");
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "protection-keys")))
            .SetApplicationName("Fuse.Inventory");
        services.AddAuthentication().AddCookie("EntraUnusedCookie").AddOpenIdConnect(Scheme, _ => { });
        services.AddOptions<OpenIdConnectOptions>(Scheme).Configure<IFuseStore, IDataProtectionProvider>((options, store, protection) =>
        {
            // The store is loaded during application startup. Options are invalidated when settings change.
            var settings = store.Current?.SecurityContext.Entra ?? new EntraSettings();
            options.SignInScheme = "EntraUnusedCookie";
            options.Authority = $"https://login.microsoftonline.com/{settings.TenantId}/v2.0";
            options.ClientId = settings.ClientId.ToString();
            options.ClientSecret = string.IsNullOrEmpty(settings.ProtectedClientSecret) ? "" :
                protection.CreateProtector(SecretPurpose).Unprotect(settings.ProtectedClientSecret);
            options.CallbackPath = "/auth/entra/callback";
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.ResponseMode = OpenIdConnectResponseMode.Query;
            options.UsePkce = true;
            options.MapInboundClaims = false;
            options.SaveTokens = false;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidIssuer = $"https://login.microsoftonline.com/{settings.TenantId}/v2.0",
                ValidAudience = settings.ClientId.ToString(),
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "name"
            };
            // Query callback allows Lax correlation cookies, including HTTP localhost development.
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = settings.PublicOrigin.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            options.NonceCookie.SecurePolicy = options.CorrelationCookie.SecurePolicy;
            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context =>
                {
                    context.ProtocolMessage.RedirectUri = settings.PublicOrigin + options.CallbackPath;
                    context.ProtocolMessage.Prompt = "select_account";
                    context.Properties.Items["configuration"] = settings.Version.ToString();
                    return Task.CompletedTask;
                },
                OnAuthorizationCodeReceived = context =>
                {
                    if (context.TokenEndpointRequest is not null)
                        context.TokenEndpointRequest.RedirectUri = settings.PublicOrigin + options.CallbackPath;
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var result = ReadIdentity(context.Principal!, settings.TenantId);
                    if (!result.IsSuccess)
                        context.Fail(result.Error!);
                    else
                        context.HttpContext.Items[typeof(VerifiedEntraIdentity)] = result.Value;
                    return Task.CompletedTask;
                },
                OnTicketReceived = async context =>
                {
                    context.HandleResponse();
                    // OIDC claim actions remove protocol claims such as exp after TokenValidated.
                    // Carry the already-validated identity within this callback request.
                    var identity = context.HttpContext.Items[typeof(VerifiedEntraIdentity)] as VerifiedEntraIdentity;
                    if (identity is null || !Guid.TryParse(context.Properties?.GetString("configuration"), out var version))
                    {
                        context.Response.Redirect("/?entraError=claims");
                        return;
                    }
                    Guid? linkId = Guid.TryParse(context.Properties?.GetString("linkUser"), out var id) ? id : null;
                    int? linkVersion = int.TryParse(context.Properties?.GetString("linkVersion"), out var revision) ? revision : null;
                    var service = context.HttpContext.RequestServices.GetRequiredService<IEntraIdentityService>();
                    var session = await service.SignIn(identity, version, linkId, linkVersion);
                    if (!session.IsSuccess)
                    {
                        context.Response.Redirect("/?entraError=access");
                        return;
                    }
                    var tickets = context.HttpContext.RequestServices.GetRequiredService<EntraFlowTickets>();
                    var ticket = tickets.Issue(new EntraFlowTicket(session.Value!.Token, null, null));
                    context.Response.Cookies.Append(HandoffCookie, ticket, CookieOptions(context.Request, settings.PublicOrigin));
                    context.Response.Redirect("/?entra=complete");
                },
                OnRemoteFailure = context =>
                {
                    context.HandleResponse();
                    // Provider errors can include credentials/PII. Do not reflect them into the URL or logs.
                    context.Response.Redirect("/?entraError=signin");
                    return Task.CompletedTask;
                }
            };
        });
    }

    public static CookieOptions CookieOptions(HttpRequest request, string? publicOrigin = null) => new()
    {
        HttpOnly = true,
        Secure = request.IsHttps || publicOrigin?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = TimeSpan.FromMinutes(5),
        IsEssential = true
    };

    public static Result<VerifiedEntraIdentity> ReadIdentity(ClaimsPrincipal principal, Guid tenantId)
    {
        if (!Guid.TryParse(principal.FindFirst("tid")?.Value, out var tenant) || tenant != tenantId ||
            !Guid.TryParse(principal.FindFirst("oid")?.Value, out var objectId) || objectId == Guid.Empty ||
            !long.TryParse(principal.FindFirst("exp")?.Value, out var expiry))
            return Result<VerifiedEntraIdentity>.Failure("Invalid Entra identity.");
        // Fail closed on group overage. Never fetch an endpoint supplied inside a token.
        // Graph resolution can be added with explicit directory consent; no stale grants are retained.
        if (principal.HasClaim(c => c.Type == "hasgroups" || c.Type == "_claim_names" && c.Value.Contains("groups")))
            return Result<VerifiedEntraIdentity>.Failure("Entra group overage requires group filtering or Graph configuration.");
        var groups = new List<Guid>();
        foreach (var claim in principal.FindAll("groups"))
        {
            if (!Guid.TryParse(claim.Value, out var groupId))
                return Result<VerifiedEntraIdentity>.Failure("Invalid Entra group claim.");
            groups.Add(groupId);
        }
        DateTime expires;
        try
        {
            expires = DateTimeOffset.FromUnixTimeSeconds(expiry).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result<VerifiedEntraIdentity>.Failure("Invalid expiry.");
        }
        return Result<VerifiedEntraIdentity>.Success(new(tenant, objectId,
            principal.FindFirst("name")?.Value ?? "Entra user", groups, expires));
    }
}
