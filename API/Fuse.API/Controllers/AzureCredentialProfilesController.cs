using Fuse.Core.Areas.AzureMetadata;
using Fuse.Core.Areas.SecretProvider;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/azure-credential-profiles")]
public sealed class AzureCredentialProfilesController(AzureCredentialProfiles profiles) : ControllerBase
{
    [HttpGet, RequirePermissionKey(SecretProviderPermissions.ReadKey)]
    public async Task<ActionResult<IReadOnlyList<AzureProfileInfo>>> List(CancellationToken ct) => Ok(await profiles.ListAsync(ct));

    [HttpPost, RequirePermissionKey(SecretProviderPermissions.ProfileManageKey)]
    public async Task<ActionResult<AzureProfileInfo>> Create(SaveAzureProfile body, CancellationToken ct) => await Save(null, body, ct);

    [HttpPut("{id:guid}"), RequirePermissionKey(SecretProviderPermissions.ProfileManageKey)]
    public async Task<ActionResult<AzureProfileInfo>> Update(Guid id, SaveAzureProfile body, CancellationToken ct) => await Save(id, body, ct);

    private async Task<ActionResult<AzureProfileInfo>> Save(Guid? id, SaveAzureProfile body, CancellationToken ct)
    {
        var result = await profiles.Save(id, body, ct);
        if (result.IsSuccess) return Ok(result.Value);
        return StatusCode(Status(result.ErrorType), new { error = result.Error });
    }

    [HttpDelete("{id:guid}"), RequirePermissionKey(SecretProviderPermissions.ProfileManageKey)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await profiles.Remove(id, ct);
        return result.IsSuccess ? NoContent() : StatusCode(Status(result.ErrorType), new { error = result.Error });
    }

    private static int Status(Fuse.Core.Helpers.ErrorType? error) => error switch
    {
        Fuse.Core.Helpers.ErrorType.NotFound => 404,
        Fuse.Core.Helpers.ErrorType.Conflict => 409,
        _ => 400
    };
}
