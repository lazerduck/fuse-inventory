using Fuse.API.CurrentUser;
using Fuse.Core.Areas.SecretProvider;
using Fuse.Core.Commands;
using Fuse.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/azure-discovery")]
public class AzureDiscoveryController(IAzureDiscoveryService discovery) : ControllerBase
{
    [HttpPost("subscriptions")]
    [RequirePermissionKey(SecretProviderPermissions.CreateKey)]
    [ProducesResponseType(200, Type = typeof(AzureSubscriptionsResponse))]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Subscriptions(AzureDiscoveryAuthentication command, CancellationToken ct)
    {
        var result = await discovery.ListSubscriptionsAsync(command.AuthMode, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPost("resources")]
    [RequirePermissionKey(SecretProviderPermissions.CreateKey)]
    [ProducesResponseType(200, Type = typeof(AzureResourcesResponse))]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Resources(DiscoverAzureResources command, CancellationToken ct)
    {
        var result = await discovery.DiscoverAsync(command, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPost("import")]
    [RequirePermissionKey(SecretProviderPermissions.CreateKey)]
    [ProducesResponseType(200, Type = typeof(AzureImportResponse))]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Import(ImportAzureResources command, CancellationToken ct)
    {
        var result = await discovery.ImportAsync(command, User.GetUsername() ?? "Anonymous", User.GetPrincipalId(), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPut("exclusions")]
    [RequirePermissionKey(SecretProviderPermissions.CreateKey)]
    [ProducesResponseType(200, Type = typeof(AzureDiscoveryExclusionsResponse))]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Exclusions(UpdateAzureDiscoveryExclusions command, CancellationToken ct)
    {
        var result = await discovery.UpdateExclusionsAsync(command, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
