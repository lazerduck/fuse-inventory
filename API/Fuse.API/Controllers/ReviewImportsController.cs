using System.Security.Cryptography;
using System.Text.Json;
using Fuse.API.CurrentUser;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using AccountAccess = Fuse.Core.Areas.Account.AccountPermissions;
using AppAccess = Fuse.Core.Areas.Application.ApplicationPermissions;
using BrokerAccess = Fuse.Core.Areas.MessageBroker.MessageBrokerPermissions;
using StoreAccess = Fuse.Core.Areas.DataStore.DataStorePermissions;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/IntegrationReviews/imports")]
public sealed class ReviewImportsController(ReviewImportService imports, IFuseStore store, RequestPermissions access,
    IDataProtectionProvider protection) : ControllerBase
{
    private bool Can(Snapshot s, string key) => access.Allows(User, s, key);
    private bool ReadSource(Snapshot s, string provider) => Can(s, IntegrationReviewPermissions.ImportKey) &&
        Can(s, IntegrationReviewPermissions.ReadKey) && Can(s, provider + "integrations:read");
    private bool CanRead(Snapshot s, ImportSelection item)
    {
        if (!ReadSource(s, item.Provider) || item.Item is null || item.InstanceId.HasValue && !Can(s, AppAccess.ReadKey)) return false;
        var target = item.Provider switch {
            "rabbitmq" or "servicebus" => BrokerAccess.ReadKey, "keycloak" => AppAccess.ReadKey,
            "redis" or "postgresql" or "mongodb" => StoreAccess.ReadKey, _ => null
        };
        return target is not null && Can(s, target) && (item.Item.Kind == "queue" || Can(s, AccountAccess.ReadKey));
    }
    private bool CanWrite(Snapshot s, ImportPreview p)
    {
        if (!CanRead(s, p.Selection) || p.LinkDependency && !Can(s, AppAccess.UpdateInstanceKey)) return false;
        if (p.ExistingId.HasValue && !p.RecordOrigin) return true;
        return Can(s, p.Selection.Item.Kind == "queue" ? BrokerAccess.UpdateKey : p.RecordOrigin ? AccountAccess.UpdateKey : AccountAccess.CreateKey);
    }
    private ObjectResult Failed(Fuse.Core.Helpers.IResult result) => StatusCode(result.ErrorType switch {
        ErrorType.NotFound => 404, ErrorType.Unauthorized => 403, ErrorType.Conflict => 409, _ => 400
    }, new { error = result.Error });

    private ITimeLimitedDataProtector Signer() => protection.CreateProtector("Fuse.ReviewImports.v1",
        User.GetPrincipalId()?.ToString() ?? "anonymous").ToTimeLimitedDataProtector();

    [HttpGet, SwaggerOperation(OperationId = "reviewImportChoices"), RequirePermissionKey(IntegrationReviewPermissions.ImportKey)]
    public async Task<ActionResult<IReadOnlyList<ObservedItem>>> Choices([FromQuery] string provider, [FromQuery] Guid integrationId, CancellationToken ct)
    {
        var s = await store.GetAsync(ct);
        var kind = provider is "rabbitmq" or "servicebus" ? "queue" : provider == "keycloak" ? "client" : "account";
        if (!CanRead(s, new(provider, integrationId, new(kind, "", "")))) return StatusCode(403);
        if (!ReviewSources.All(s).Any(x => x.Provider == provider && x.Id == integrationId)) return NotFound();
        var items = await imports.DiscoverAsync(provider, integrationId, ct);
        return Ok(items.Where(i => CanRead(s, new(provider, integrationId, i))));
    }

    [HttpPost("preview"), SwaggerOperation(OperationId = "reviewImportPreview"), RequirePermissionKey(IntegrationReviewPermissions.ImportKey)]
    public async Task<ActionResult<ImportPreview>> Preview(ImportSelection selection, CancellationToken ct)
    {
        if (!CanRead(await store.GetAsync(ct), selection)) return StatusCode(403);
        var result = await imports.PreviewAsync(selection, ct);
        if (!result.IsSuccess) return Failed(result);
        var preview = result.Value!;
        if (!CanWrite(await store.GetAsync(ct), preview)) return StatusCode(403);
        var token = Signer().Protect(JsonSerializer.Serialize(preview), TimeSpan.FromMinutes(10));
        return Ok(preview with { ConfirmationToken = token });
    }

    [HttpPost("confirm"), SwaggerOperation(OperationId = "reviewImportConfirm"), RequirePermissionKey(IntegrationReviewPermissions.ImportKey)]
    public async Task<ActionResult<ImportReceipt>> Confirm(ImportPreview preview, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(preview.ConfirmationToken) || preview.ConfirmationToken.Length > 32768) return Conflict(new { error = "Preview the import again." });
        ImportPreview? approved;
        try { approved = JsonSerializer.Deserialize<ImportPreview>(Signer().Unprotect(preview.ConfirmationToken)); }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException)
        { return Conflict(new { error = "The import preview expired or changed. Preview again." }); }
        // Browsers round-trip DateTime at millisecond precision. Use the protected server timestamp for concurrency.
        if (approved is null || approved != (preview with { ConnectionVersion = approved.ConnectionVersion, ConfirmationToken = null }))
            return Conflict(new { error = "The import preview changed. Preview again." });
        if (!CanWrite(await store.GetAsync(ct), approved)) return StatusCode(403);
        var result = await imports.ApplyAsync(approved, ct, CanWrite);
        return result.IsSuccess ? Ok(result.Value) : Failed(result);
    }
}
