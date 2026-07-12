using Fuse.Core.Areas.Logging;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Mvc;
using SystemLogLevel = Fuse.Core.Models.LogLevel;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LoggingController(ILogService logService) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(OperationId = "querySystemLogs")]
    [RequirePermissionKey(LoggingPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(SystemLogResult))]
    public async Task<ActionResult<SystemLogResult>> QueryLogs(
        [FromQuery] SystemLogLevel? minLevel,
        [FromQuery] string? area,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] string? searchText,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await logService.QueryAsync(new SystemLogQuery
        {
            MinLevel = minLevel,
            Area = area,
            StartTime = startTime,
            EndTime = endTime,
            SearchText = searchText,
            Page = page,
            PageSize = pageSize
        });

        return Ok(result);
    }

    [HttpGet("counts")]
    [SwaggerOperation(OperationId = "getSystemLogCounts")]
    [RequirePermissionKey(LoggingPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(SystemLogCounts))]
    public async Task<ActionResult<SystemLogCounts>> GetCounts(
        [FromQuery] SystemLogLevel? minLevel,
        [FromQuery] string? area,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] string? searchText)
    {
        var result = await logService.GetCountsAsync(new SystemLogQuery
        {
            MinLevel = minLevel,
            Area = area,
            StartTime = startTime,
            EndTime = endTime,
            SearchText = searchText
        });

        return Ok(result);
    }

    [HttpGet("areas")]
    [SwaggerOperation(OperationId = "getSystemLogAreas")]
    [RequirePermissionKey(LoggingPermissions.ReadKey)]
    [ProducesResponseType(200, Type = typeof(IEnumerable<string>))]
    public async Task<ActionResult<IEnumerable<string>>> GetAreas()
    {
        var areas = await logService.GetAreasAsync();
        return Ok(areas);
    }

    [HttpPost("cleanup")]
    [SwaggerOperation(OperationId = "cleanupSystemLogs")]
    [RequirePermissionKey(LoggingPermissions.ReadKey)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Cleanup()
    {
        await logService.CleanupOldLogsAsync();
        return NoContent();
    }
}
