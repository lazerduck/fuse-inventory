using Fuse.API.CurrentUser;
using Fuse.Core.Areas.Application;
using Fuse.Core.Interfaces;
using Fuse.Core.Areas.Activity;
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
    [RequirePermissionKey(ActivityPermissions.ReadKey)]
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

    [HttpGet("applications/changes/{versionId:guid}")]
    [SwaggerOperation(OperationId = "applicationChanges")]
    [RequirePermissionKey(ActivityPermissions.ReadKey)]
    [ProducesResponseType<ApplicationChanges>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationChanges>> GetApplicationChanges(Guid versionId,
        [FromServices] IVersionHistoryService history, [FromServices] IFuseStore store, CancellationToken ct)
    {
        var inventory = await store.GetAsync(ct);
        // These inventory read permissions are public in both open-read postures.
        var openRead = User.IsAdmin() || inventory.SecurityContext.Posture != SecurityPosture.FullyRestricted;
        var roleIds = User.GetRoleIds();
        var permissions = inventory.SecurityContext.Roles.Where(r => roleIds.Contains(r.Id))
            .SelectMany(r => r.Permissions).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool CanRead(string key) => openRead || permissions.Contains(key);
        if (!CanRead(ApplicationPermissions.ReadKey))
            return StatusCode(StatusCodes.Status403Forbidden);

        var selected = await history.GetVersionByIdAsync(versionId, ct);
        if (selected?.EntityType != EntityType.Application)
            return NotFound(new { error = "This application history entry is unavailable." });

        var before = selected.InitialApplicationSnapshot;
        if (before is null && selected.Action != AuditAction.ApplicationCreated && selected.Version > 1)
        {
            var adjacent = await history.QueryAsync(new VersionHistoryQuery(selected.EntityId, EntityType.Application, pageSize: 2)
            {
                MinVersion = selected.Version - 1,
                MaxVersion = selected.Version
            }, ct);
            // Older stores can have duplicate revision numbers. Never guess a baseline.
            if (adjacent.TotalCount == 2 && adjacent.Versions.Any(v => v.Id == selected.Id))
                before = adjacent.Versions.FirstOrDefault(v => v.Version == selected.Version - 1)?.EntitySnapshot;
        }
        return Ok(new ApplicationChangeComparison(inventory, CanRead).Compare(selected, before));
    }

    [HttpGet("{entityType}/{entityId:guid}")]
    [SwaggerOperation(OperationId = "activityByEntity")]
    [RequirePermissionKey(ActivityPermissions.ReadKey)]
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
