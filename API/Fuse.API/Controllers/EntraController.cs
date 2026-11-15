using Fuse.API.CurrentUser;
using Fuse.API.Entra;
using Fuse.Core.Areas.Security;
using Fuse.Core.Areas.Security.Entra;
using Fuse.Core.Areas.Security.Interfaces;
using Fuse.Core.Areas.Security.Permissions;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Fuse.API.Controllers;

[ApiController]
public class EntraController(
    IFuseStore store,
    IEntraSettingsService settingsService,
    IOptionsMonitorCache<OpenIdConnectOptions> optionsCache,
    EntraFlowTickets tickets,
    IFuseUserService users,
    IFuseUserSessionService sessions,
    IAntiforgery antiforgery,
    IEntraIdentityService identities) : ControllerBase
{
    [HttpGet("api/entra/status")]
    [SwaggerOperation(OperationId = "entraStatus")]
    [AllowWithoutPermission, AllowDuringSetup]
    [ProducesResponseType(200, Type = typeof(EntraStatusInfo))]
    public async Task<ActionResult<EntraStatusInfo>> Status()
    {
        var settings = await settingsService.GetSettings();
        return Ok(new EntraStatusInfo(settings.Enabled));
    }

    [HttpGet("api/entra/settings")]
    [SwaggerOperation(OperationId = "entraSettings")]
    [RequirePermissionKey(SecuritySettingsPermissions.UpdateSettingsKey)]
    [ProducesResponseType(200, Type = typeof(EntraSettingsInfo))]
    [ProducesResponseType(401, Type = typeof(EntraErrorInfo))]
    [ProducesResponseType(403)]
    public async Task<ActionResult<EntraSettingsInfo>> Settings()
    {
        return Ok(await settingsService.GetSettings());
    }

    [HttpPut("api/entra/settings")]
    [SwaggerOperation(OperationId = "entraSettingsUpdate")]
    [RequirePermissionKey(SecuritySettingsPermissions.UpdateSettingsKey)]
    [ProducesResponseType(204)]
    [ProducesResponseType(400, Type = typeof(EntraErrorInfo))]
    [ProducesResponseType(401, Type = typeof(EntraErrorInfo))]
    [ProducesResponseType(403)]
    public async Task<IActionResult> SaveSettings([FromBody] UpdateEntraSettings command)
    {
        var result = await settingsService.UpdateSettings(command);
        if (!result.IsSuccess)
            return ToAction(result);

        optionsCache.TryRemove(EntraAuthentication.Scheme);
        return NoContent();
    }

    [HttpGet("auth/entra/login")]
    [AllowWithoutPermission, ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Login()
    {
        var security = (await store.GetAsync()).SecurityContext;
        if (!security.Entra.Enabled || !security.Users.Any(u => u.IsAdmin))
            return NotFound();
        var properties = new AuthenticationProperties { RedirectUri = "/" };
        var link = tickets.Take(Request.Cookies[EntraAuthentication.LinkCookie]);
        Response.Cookies.Delete(EntraAuthentication.LinkCookie);
        if (link?.LinkUserId is { } userId)
        {
            properties.Items["linkUser"] = userId.ToString();
            properties.Items["linkVersion"] = link.LinkSessionVersion.ToString();
        }
        return Challenge(properties, EntraAuthentication.Scheme);
    }

    [HttpPost("api/entra/link")]
    [SwaggerOperation(OperationId = "entraLink")]
    [AllowWithoutPermission]
    [ProducesResponseType(200, Type = typeof(EntraLinkInfo))]
    [ProducesResponseType(400, Type = typeof(EntraErrorInfo))]
    [ProducesResponseType(401, Type = typeof(EntraErrorInfo))]
    public async Task<ActionResult<EntraLinkInfo>> Link([FromBody] LinkEntraAccount command)
    {
        var userId = User.GetPrincipalId();
        if (!User.IsUserAuth() || userId is null)
            return Unauthorized();
        var user = (await users.GetUser(userId.Value)).Value;
        if (user is null || user.IsDisabled || !(await users.VerifyUser(user.UserName, command.CurrentPassword)).IsSuccess)
            return Unauthorized(new EntraErrorInfo("Confirm your local password to link Microsoft sign-in."));
        var settings = (await store.GetAsync()).SecurityContext.Entra;
        if (!settings.Enabled)
            return BadRequest();
        var ticket = tickets.Issue(new EntraFlowTicket(null, user.Id, user.SessionVersion));
        Response.Cookies.Append(EntraAuthentication.LinkCookie, ticket, EntraAuthentication.CookieOptions(Request, settings.PublicOrigin));
        return Ok(new EntraLinkInfo("/auth/entra/login"));
    }

    [HttpGet("auth/entra/antiforgery")]
    [AllowWithoutPermission]
    [SwaggerOperation(OperationId = "entraAntiforgery")]
    [ProducesResponseType(200, Type = typeof(EntraAntiforgeryInfo))]
    public ActionResult<EntraAntiforgeryInfo> Antiforgery()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new EntraAntiforgeryInfo(tokens.RequestToken!));
    }

    [HttpPost("auth/entra/exchange")]
    [AllowWithoutPermission]
    [SwaggerOperation(OperationId = "entraExchange")]
    [ProducesResponseType(200, Type = typeof(LoginSession))]
    [ProducesResponseType(400, Type = typeof(EntraErrorInfo))]
    [ProducesResponseType(401, Type = typeof(EntraErrorInfo))]
    public async Task<ActionResult<LoginSession>> Exchange([FromHeader(Name = "X-Fuse-Entra")] string requestToken)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest(new EntraErrorInfo("Invalid sign-in antiforgery token. Please retry Microsoft sign-in."));
        }
        var ticket = tickets.Take(Request.Cookies[EntraAuthentication.HandoffCookie]);
        Response.Cookies.Delete(EntraAuthentication.HandoffCookie);
        if (ticket?.SessionToken is not { } token)
            return Unauthorized();
        var validation = await sessions.ValidateSession(token);
        if (!validation.IsSuccess)
            return Unauthorized();
        var user = (await users.GetUser(validation.Value)).Value!;
        Response.Headers.CacheControl = "no-store";
        return Ok(new LoginSession(token, (await sessions.GetExpiry(token)).Value, new SecurityUserInfo(user)));
    }

    [HttpPost("api/entra/users/{id:guid}/revoke")]
    [SwaggerOperation(OperationId = "entraRevoke")]
    [RequirePermissionKey(UserAccountPermissions.UpdateKey)]
    [ProducesResponseType(204)]
    [ProducesResponseType(400, Type = typeof(EntraErrorInfo))]
    [ProducesResponseType(401, Type = typeof(EntraErrorInfo))]
    [ProducesResponseType(403)]
    [ProducesResponseType(404, Type = typeof(EntraErrorInfo))]
    public async Task<IActionResult> Revoke([FromRoute] Guid id)
    {
        return ToAction(await identities.Revoke(id));
    }

    [HttpPut("api/entra/users/{id:guid}/disabled")]
    [SwaggerOperation(OperationId = "entraDisabled")]
    [RequirePermissionKey(UserAccountPermissions.UpdateKey)]
    [ProducesResponseType(204)]
    [ProducesResponseType(400, Type = typeof(EntraErrorInfo))]
    [ProducesResponseType(401, Type = typeof(EntraErrorInfo))]
    [ProducesResponseType(403)]
    [ProducesResponseType(404, Type = typeof(EntraErrorInfo))]
    public async Task<IActionResult> Disable([FromRoute] Guid id, [FromBody] SetUserDisabled command)
    {
        return ToAction(await identities.Revoke(id, command.Disabled));
    }

    private IActionResult ToAction(Result result)
    {
        if (result.IsSuccess)
            return NoContent();

        return result.ErrorType switch
        {
            ErrorType.NotFound => NotFound(new EntraErrorInfo(result.Error)),
            ErrorType.Conflict => Conflict(new EntraErrorInfo(result.Error)),
            ErrorType.Unauthorized => Unauthorized(new EntraErrorInfo(result.Error)),
            _ => BadRequest(new EntraErrorInfo(result.Error))
        };
    }
}
