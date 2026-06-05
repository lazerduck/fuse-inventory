using Fuse.Core.Areas.AppSettings;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppSettingsController(IAppSettingsService appSettingsService) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(OperationId = "getAppSettings")]
    [ProducesResponseType(200, Type = typeof(AppSettings))]
    public async Task<ActionResult<AppSettings>> GetAppSettings()
    {
        var settings = await appSettingsService.GetAppSettingsAsync();
        return Ok(settings);
    }

    [HttpPut]
    [SwaggerOperation(OperationId = "updateAppSettings")]
    [RequirePermissionKey(AppSettingsPermissions.UpdateKey)]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateAppSettings([FromBody] AppSettings updatedSettings)
    {
        if (updatedSettings is null)
            return BadRequest(new { error = "Invalid settings data." });

        await appSettingsService.UpdateAppSettingsAsync(updatedSettings);
        return NoContent();
    }
}