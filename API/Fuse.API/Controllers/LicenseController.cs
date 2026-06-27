using Fuse.Core.Areas.License;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LicenseController(ILicenseService licenseService) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(OperationId = "getLicenseStatus")]
    public Task<LicenseStatusResponse> Get(CancellationToken ct) => licenseService.GetStatusAsync(ct);

    [HttpPut]
    [RequirePermissionKey(LicensePermissions.UpdateKey)]
    [SwaggerOperation(OperationId = "setLicense")]
    public async Task<ActionResult<LicenseStatusResponse>> Put(SetLicenseRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.LicenseKey)) return BadRequest(new { error = "A license key is required." });
        return Ok(await licenseService.SetLicenseAsync(request.LicenseKey, ct));
    }

    [HttpPost("refresh")]
    [RequirePermissionKey(LicensePermissions.UpdateKey)]
    [SwaggerOperation(OperationId = "refreshLicense")]
    public async Task<ActionResult<LicenseStatusResponse>> Refresh(CancellationToken ct)
    {
        await licenseService.RefreshOnlineAsync(ct);
        return Ok(await licenseService.GetStatusAsync(ct));
    }
}
