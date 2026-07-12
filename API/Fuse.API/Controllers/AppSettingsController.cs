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

        if (updatedSettings.VersionHistoryKeepCount is < 0 or > 10000)
            return BadRequest(new { error = "Version history limit must be between 0 and 10,000." });

        if (updatedSettings.AuditLogDaysToKeep is < 0 or > 36500)
            return BadRequest(new { error = "Audit log retention must be between 0 and 36,500 days." });

        if (updatedSettings.Logging?.DaysToKeep is < 0 or > 36500)
            return BadRequest(new { error = "System log retention must be between 0 and 36,500 days." });

        if (!Enum.IsDefined(updatedSettings.HealthCheckProvider))
            return BadRequest(new { error = "Invalid health check provider." });

        if (updatedSettings.Logging is not null && !Enum.IsDefined(updatedSettings.Logging.MinLevel))
            return BadRequest(new { error = "Invalid system log level." });

        await appSettingsService.UpdateAppSettingsAsync(updatedSettings);
        return NoContent();
    }
}
