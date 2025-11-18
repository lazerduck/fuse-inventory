using Microsoft.AspNetCore.Mvc;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>
    /// Query audit logs with filtering and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(AuditLogResult))]
    public async Task<ActionResult<AuditLogResult>> QueryAuditLogs(
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] AuditAction? action,
        [FromQuery] AuditArea? area,
        [FromQuery] string? userName,
        [FromQuery] Guid? entityId,
        [FromQuery] string? searchText,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new AuditLogQuery
        {
            StartTime = startTime,
            EndTime = endTime,
            Action = action,
            Area = area,
            UserName = userName,
            EntityId = entityId,
            SearchText = searchText,
            Page = page,
            PageSize = pageSize
        };

        var result = await _auditService.QueryAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific audit log by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(200, Type = typeof(AuditLog))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<AuditLog>> GetAuditLog([FromRoute] Guid id)
    {
        var log = await _auditService.GetByIdAsync(id);
        if (log == null)
        {
            return NotFound(new { error = $"Audit log with ID '{id}' not found." });
        }
        return Ok(log);
    }

    /// <summary>
    /// Get all available audit actions
    /// </summary>
    [HttpGet("actions")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<string>))]
    public ActionResult<IEnumerable<string>> GetAuditActions()
    {
        var actions = Enum.GetNames(typeof(AuditAction));
        return Ok(actions);
    }

    /// <summary>
    /// Get all available audit areas
    /// </summary>
    [HttpGet("areas")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<string>))]
    public ActionResult<IEnumerable<string>> GetAuditAreas()
    {
        var areas = Enum.GetNames(typeof(AuditArea));
        return Ok(areas);
    }
}
