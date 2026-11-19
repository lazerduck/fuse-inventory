using Fuse.Core.Areas.Application;
using Fuse.Core.Areas.Environment;
using Fuse.Core.Areas.Identity;
using Fuse.Core.Areas.Position;
using Fuse.Core.Areas.Responsibility;
using Fuse.Core.Areas.Security;
using Fuse.API.CurrentUser;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Mvc;
using static Fuse.Core.Areas.IntegrationReviews.IntegrationReviewPermissions;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class IntegrationReviewsController(IFuseStore store, IntegrationReviewService service, RequestPermissions permissions) : ControllerBase
{
    private bool Can(Snapshot snapshot, string key) => permissions.Allows(User, snapshot, key);
    private bool CanRead(Snapshot s, ReviewInput input) => Can(s, ReadKey) && Can(s, input.Provider + "integrations:read")
        && (input.AccountId is null || Can(s, AreaPermissions.ReadKey))
        && (input.Fields?.ContainsKey("identityId") != true || Can(s, IdentityPermissions.ReadKey));
    private ReviewDefinition Public(Snapshot s, ReviewDefinition row) => Can(s, ApplicationPermissions.ReadKey) ? row : row with { ApplicationInstanceIds = [] };
    private IReadOnlyList<ReviewApplication> Applications(Snapshot s, ReviewDefinition row) =>
        Can(s, ApplicationPermissions.ReadKey) ? ReviewSources.Applications(s, row, Can(s, EnvironmentPermissions.ReadKey),
            Can(s, ResponsibilityPermissions.ReadKey) && Can(s, PositionPermissions.ReadKey)) : [];
    private ObjectResult Failed(Fuse.Core.Helpers.IResult result) => StatusCode(result.ErrorType switch
    {
        ErrorType.NotFound => 404, ErrorType.Conflict => 409, ErrorType.Unauthorized => 403, _ => 400
    }, new { error = result.Error });

    [HttpGet("catalog"), SwaggerOperation(OperationId = "reviewsCatalog"), RequirePermissionKey(ReadKey)]
    public async Task<ActionResult<IReadOnlyList<ReviewCatalog>>> Catalog(CancellationToken ct)
    {
        var s = await store.GetAsync(ct);
        var sources = ReviewSources.All(s).ToLookup(x => x.Provider);
        return Ok(service.Providers.Values.Where(p => Can(s, p.Provider + "integrations:read")).OrderBy(p => p.Provider)
            .Select(p => new ReviewCatalog(p.Provider, p.Checks, sources[p.Provider].OrderBy(x => x.Name).ToArray())));
    }

    [HttpGet, SwaggerOperation(OperationId = "reviewsList"), RequirePermissionKey(ReadKey)]
    public async Task<ActionResult<IReadOnlyList<ReviewView>>> List([FromQuery] Guid? accountId, [FromQuery] Guid? applicationId,
        [FromQuery] Guid? instanceId, [FromQuery] Guid? targetId, CancellationToken ct)
    {
        var s = await store.GetAsync(ct);
        if ((applicationId.HasValue || instanceId.HasValue) && !Can(s, ApplicationPermissions.ReadKey)
            || accountId.HasValue && !Can(s, AreaPermissions.ReadKey)) return StatusCode(403);
        var rows = s.IntegrationReviews.Where(r => CanRead(s, r.Input) && (!accountId.HasValue || r.Input.AccountId == accountId)
            && (!targetId.HasValue || ReviewSources.Find(s, r.Input)?.TargetId == targetId))
            .Select(r => new ReviewView(Public(s, r), Applications(s, r)))
            .Where(v => !applicationId.HasValue && !instanceId.HasValue || v.Applications.Any(a =>
                (!applicationId.HasValue || a.ApplicationId == applicationId) && (!instanceId.HasValue || a.InstanceId == instanceId)));
        return Ok(rows.OrderBy(v => v.Review.Name));
    }

    [HttpGet("{id:guid}"), SwaggerOperation(OperationId = "reviewsGet"), RequirePermissionKey(ReadKey)]
    public async Task<ActionResult<ReviewDefinition>> Get(Guid id, CancellationToken ct)
    {
        var s = await store.GetAsync(ct);
        var row = s.IntegrationReviews.FirstOrDefault(x => x.Id == id);
        return row is null || !CanRead(s, row.Input) ? NotFound() : Ok(Public(s, row));
    }

    [ProducesResponseType(201, Type = typeof(ReviewDefinition))]
    [HttpPost, SwaggerOperation(OperationId = "reviewsCreate"), RequirePermissionKey(ManageKey)]
    public Task<ActionResult<ReviewDefinition>> Create(SaveReview body, CancellationToken ct) => Save(null, body, ct);

    [HttpPut("{id:guid}"), SwaggerOperation(OperationId = "reviewsUpdate"), RequirePermissionKey(ManageKey)]
    public Task<ActionResult<ReviewDefinition>> Update(Guid id, SaveReview body, CancellationToken ct) => Save(id, body, ct);

    private async Task<ActionResult<ReviewDefinition>> Save(Guid? id, SaveReview body, CancellationToken ct)
    {
        var s = await store.GetAsync(ct);
        if (body.Input?.Provider is null || !service.Providers.ContainsKey(body.Input.Provider)) return BadRequest(new { error = "Unknown provider." });
        if (!CanRead(s, body.Input)) return StatusCode(403);
        var old = id.HasValue ? s.IntegrationReviews.FirstOrDefault(x => x.Id == id) : null;
        if (id.HasValue && (old is null || !CanRead(s, old.Input))) return NotFound();
        if ((body.ApplicationInstanceIds is { Length: > 0 } || old?.ApplicationInstanceIds.Length > 0) && !Can(s, ApplicationPermissions.ReadKey)
            || body.RefreshMinutes > 0 && !Can(s, RefreshKey)) return StatusCode(403);
        var result = await service.SaveAsync(id, body, ct);
        if (!result.IsSuccess) return Failed(result);
        return id.HasValue ? Ok(Public(s, result.Value!)) : CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, Public(s, result.Value));
    }

    [HttpPost("{id:guid}/refresh"), SwaggerOperation(OperationId = "reviewsRefresh"), RequirePermissionKey(RefreshKey)]
    public async Task<ActionResult<ReviewDefinition>> Inspect(Guid id, CancellationToken ct)
    {
        var s = await store.GetAsync(ct);
        if (s.IntegrationReviews.FirstOrDefault(x => x.Id == id) is not {} row || !CanRead(s, row.Input)) return NotFound();
        var result = await service.RefreshAsync(id, ct);
        return result.IsSuccess ? Ok(Public(s, result.Value!)) : Failed(result);
    }

    [HttpDelete("{id:guid}"), SwaggerOperation(OperationId = "reviewsDelete"), RequirePermissionKey(ManageKey)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid revision, CancellationToken ct)
    {
        var s = await store.GetAsync(ct);
        if (s.IntegrationReviews.FirstOrDefault(x => x.Id == id) is not {} row || !CanRead(s, row.Input)) return NotFound();
        var result = await service.DeleteAsync(id, revision, ct);
        return result.IsSuccess ? NoContent() : Failed(result);
    }
}
