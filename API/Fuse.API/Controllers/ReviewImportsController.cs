using Fuse.API.CurrentUser;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/IntegrationReviews/imports")]
public sealed class ReviewImportsController(ReviewImportService imports, IFuseStore store, RequestPermissions access) : ControllerBase
{
    private bool Can(Snapshot s, string key) => access.Allows(User, s, key);
    private bool ReadSource(Snapshot s, string provider) =>
        Can(s, IntegrationReviewPermissions.ReadKey) && Can(s, provider + "integrations:read");
    private bool CanImport(Snapshot s, string kind, Guid? instance) => kind switch
    {
        "queue" => Can(s, "messagebrokers:read") && Can(s, "messagebrokers:update")
                && (instance is null || Can(s, "applications:read") && Can(s, "applications:update")),
        "client" => Can(s, "accounts:read") && Can(s, "accounts:create") && Can(s, "applications:read"),
        "account" => Can(s, "accounts:read") && Can(s, "accounts:create")
                && (instance is null || Can(s, "applications:read") && Can(s, "applications:update")),
        _ => false
    };
    private bool Allowed(Snapshot s, ImportSelection item) =>
        ReadSource(s, item.Provider) && item.Item is not null && CanImport(s, item.Item.Kind, item.InstanceId)
        && (item.Provider is "rabbitmq" or "servicebus" ? Can(s, "messagebrokers:read") :
            item.Provider == "keycloak" ? Can(s, "applications:read") : Can(s, "datastores:read"));
    private ObjectResult Failed(Fuse.Core.Helpers.IResult result) => StatusCode(result.ErrorType == ErrorType.NotFound ? 404 :
        result.ErrorType == ErrorType.Conflict ? 409 : 400, new { error = result.Error });

    [HttpGet, SwaggerOperation(OperationId = "reviewImportChoices"), RequirePermissionKey(IntegrationReviewPermissions.ImportKey)]
    public async Task<ActionResult<IReadOnlyList<ObservedItem>>> Choices([FromQuery] string provider, [FromQuery] Guid integrationId, CancellationToken ct)
    {
        var s = await store.GetAsync(ct);
        if (!ReadSource(s, provider) || !CanImport(s, "account", null) && !CanImport(s, "queue", null) && !CanImport(s, "client", null))
            return StatusCode(403);
        if (!ReviewSources.All(s).Any(x => x.Provider == provider && x.Id == integrationId)) return NotFound();
        var items = await imports.DiscoverAsync(provider, integrationId, ct);
        return Ok(items.Where(i => Allowed(s, new(provider, integrationId, i))));
    }

    [HttpPost("preview"), SwaggerOperation(OperationId = "reviewImportPreview"), RequirePermissionKey(IntegrationReviewPermissions.ImportKey)]
    public async Task<ActionResult<ImportPreview>> Preview(ImportSelection selection, CancellationToken ct)
    {
        if (!Allowed(await store.GetAsync(ct), selection)) return StatusCode(403);
        var result = await imports.PreviewAsync(selection, ct);
        return result.IsSuccess ? Ok(result.Value) : Failed(result);
    }

    [HttpPost("confirm"), SwaggerOperation(OperationId = "reviewImportConfirm"), RequirePermissionKey(IntegrationReviewPermissions.ImportKey)]
    public async Task<ActionResult<ImportReceipt>> Confirm(ImportPreview preview, CancellationToken ct)
    {
        if (preview.Selection is null || !Allowed(await store.GetAsync(ct), preview.Selection)) return StatusCode(403);
        var result = await imports.ApplyAsync(preview, ct);
        return result.IsSuccess ? Ok(result.Value) : Failed(result);
    }
}
