using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActivityController : ControllerBase
{
    private readonly IActivityFeedService _activityFeedService;

    public ActivityController(IActivityFeedService activityFeedService)
    {
        _activityFeedService = activityFeedService;
    }

    [HttpGet]
    [SwaggerOperation(OperationId = "activity")]
    [ProducesResponseType(200, Type = typeof(ActivityFeedResult))]
    public async Task<ActionResult<ActivityFeedResult>> Query(
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] EntityType? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] Guid? userId,
        [FromQuery] string? userName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new VersionHistoryQuery(
            entityId: entityId,
            entityType: entityType,
            startTime: startTime,
            endTime: endTime,
            userId: userId,
            userName: userName,
            page: page,
            pageSize: pageSize);

        var result = await _activityFeedService.QueryAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("{entityType}/{entityId:guid}")]
    [SwaggerOperation(OperationId = "activityByEntity")]
    [ProducesResponseType(200, Type = typeof(ActivityFeedResult))]
    public async Task<ActionResult<ActivityFeedResult>> QueryByEntity(
        [FromRoute] EntityType entityType,
        [FromRoute] Guid entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _activityFeedService.QueryByEntityAsync(entityType, entityId, page, pageSize, ct);
        return Ok(result);
    }
}
