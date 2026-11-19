using Fuse.Core.Areas.Account;
using Fuse.Core.Areas.Application;
using System.Security.Cryptography;
using Fuse.API.CurrentUser;
using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Areas.PostgreSqlIntegration;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/PostgreSqlIntegration/{id:guid}/grant-repair")]
public sealed class PostgreSqlGrantRepairController(PostgreSqlGrantRepairService repair, IFuseStore store,
    RequestPermissions permissions, IDataProtectionProvider protection) : ControllerBase
{
    [HttpPost("preview"), SwaggerOperation(OperationId = "postgresqlGrantRepairPreview"), RequirePermissionKey(PostgreSqlIntegrationPermissions.ApplyGrantKey)]
    [ProducesResponseType(200, Type = typeof(GrantRepairPlan)), ProducesResponseType(400), ProducesResponseType(403), ProducesResponseType(404), ProducesResponseType(409)]
    public Task<ActionResult<GrantRepairPlan>> Preview(Guid id, GrantRepairRequest request, CancellationToken ct) => Run(id, request, false, ct);

    [HttpPost("apply"), SwaggerOperation(OperationId = "postgresqlGrantRepairApply"), RequirePermissionKey(PostgreSqlIntegrationPermissions.ApplyGrantKey)]
    [ProducesResponseType(200, Type = typeof(GrantRepairPlan)), ProducesResponseType(400), ProducesResponseType(403), ProducesResponseType(404), ProducesResponseType(409)]
    public Task<ActionResult<GrantRepairPlan>> Apply(Guid id, GrantRepairRequest request, CancellationToken ct) => Run(id, request, true, ct);

    private async Task<ActionResult<GrantRepairPlan>> Run(Guid id, GrantRepairRequest request, bool apply, CancellationToken ct)
    {
        var snapshot = await store.GetAsync(ct);
        if (!permissions.Allows(User, snapshot, IntegrationReviewPermissions.ReadKey) ||
            !permissions.Allows(User, snapshot, PostgreSqlIntegrationPermissions.ReadKey) ||
            !permissions.Allows(User, snapshot, AccountPermissions.ReadKey))
            return StatusCode(403);
        var signer = protection.CreateProtector("Fuse.PostgreSqlGrantRepair.v1", id.ToString("D"), request.ReviewId.ToString("D"))
            .ToTimeLimitedDataProtector();
        string? target = null, state = null;
        if (apply)
        {
            if (request.Token is not { Length: > 0 and <= 4096 }) return BadRequest(new { error = "Preview the repair first." });
            try
            {
                var parts = signer.Unprotect(request.Token).Split('.');
                if (parts.Length != 2) return Conflict(new { error = "Preview the repair again." });
                (target, state) = (parts[0], parts[1]);
            }
            catch (CryptographicException) { return Conflict(new { error = "The preview is invalid or expired. Preview the repair again." }); }
        }
        var result = await repair.RunAsync(id, request.ReviewId, target, state, permissions.Allows(User, snapshot, ApplicationPermissions.ReadKey), ct);
        if (!result.IsSuccess)
            return StatusCode(result.ErrorType switch { ErrorType.NotFound => 404, ErrorType.Conflict => 409, _ => 400 }, new { error = result.Error });
        var value = result.Value!;
        var token = apply || !value.Plan.ChangeRequired ? null : signer.Protect(value.Target + "." + value.State, TimeSpan.FromMinutes(5));
        return Ok(value.Plan with { Token = token });
    }
}
