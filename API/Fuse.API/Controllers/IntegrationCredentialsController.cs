using Fuse.API.CurrentUser;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController, Route("api/integration-credentials/{provider}/{id:guid}")]
public sealed class IntegrationCredentialsController(IFuseStore store, IntegrationPasswords passwords) : ControllerBase
{
    private bool CanBind(Snapshot snapshot, string provider) => provider is ("redis" or "rabbitmq" or "mongodb" or "postgresql") &&
        RequestPermissions.HasExplicitGrant(User, snapshot, SecretProviderPermissions.BindIntegrationPasswordKey) &&
        RequestPermissions.HasExplicitGrant(User, snapshot, SecretProviderPermissions.RevealSecretKey) &&
        RequestPermissions.HasExplicitGrant(User, snapshot, provider + "integrations:update");

    [RequirePermissionKey(SecretProviderPermissions.BindIntegrationPasswordKey)]
    [HttpGet, SwaggerOperation(OperationId = "integrationCredentialGet")]
    public async Task<ActionResult<PasswordReferenceView>> Get(string provider, Guid id, CancellationToken ct)
    {
        var snapshot = await store.GetAsync(ct);
        if (!CanBind(snapshot, provider)) return StatusCode(403);
        return IntegrationPasswords.Find(snapshot, provider, id) is {} item ? Ok(IntegrationPasswords.View(item)) : NotFound();
    }

    [RequirePermissionKey(SecretProviderPermissions.BindIntegrationPasswordKey)]
    [HttpPut, SwaggerOperation(OperationId = "integrationCredentialBind")]
    public async Task<ActionResult<PasswordReferenceView>> Bind(string provider, Guid id, BindIntegrationPassword command, CancellationToken ct)
    {
        var result = await passwords.BindAsync(provider, id, command, s => CanBind(s, provider), ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(result.ErrorType switch {
            ErrorType.Unauthorized => 403, ErrorType.NotFound => 404, ErrorType.Conflict => 409, _ => 400
        }, new { error = result.Error });
    }
}
