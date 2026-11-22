using Fuse.API.Agents;
using Fuse.Core.Areas.Audit;
using Fuse.Core.Areas.Platform;
using Fuse.Core.Areas.PlatformAgent;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

public sealed record AgentPairingRequest(bool Replace = false);
public sealed record AgentPairingResponse(string Ticket, string Pin, DateTime ExpiresAt, AgentAssetChecksums Artifacts);
public sealed record AgentReleaseResponse(string Version, AgentAssetChecksums Artifacts);

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/platforms/{platformId:guid}/agent")]
public sealed class PlatformAgentController(
    IAgentReportingStore agents, IAuditService audit, ICurrentUser user) : ControllerBase
{
    [HttpGet("/api/platforms/agents")]
    [SwaggerOperation(OperationId = "PlatformAgentFleet")]
    [RequirePermissionKey(PlatformPermissions.ReadKey)]
    [ProducesResponseType<IReadOnlyList<AgentFleetDevice>>(200)]
    public async Task<ActionResult<IReadOnlyList<AgentFleetDevice>>> Fleet(CancellationToken ct) =>
        Ok(await agents.FleetAsync(ct));

    [HttpGet]
    [SwaggerOperation(OperationId = "PlatformAgentStatus")]
    [RequirePermissionKey(PlatformPermissions.ReadKey)]
    [ProducesResponseType<AgentReportingStatus>(200)]
    public async Task<ActionResult<AgentReportingStatus>> Status(Guid platformId, [FromQuery] Guid? nodeId, CancellationToken ct)
    {
        var status = await agents.StatusAsync(new(platformId, nodeId), ct);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpPost("pair")]
    [SwaggerOperation(OperationId = "PlatformAgentPair")]
    [RequirePermissionKey(PlatformPermissions.ManageAgentsKey)]
    [ProducesResponseType<AgentPairingResponse>(200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(503)]
    public async Task<ActionResult<AgentPairingResponse>> Pair(Guid platformId, [FromQuery] Guid? nodeId,
        [FromBody] AgentPairingRequest request, [FromServices] AgentServerIdentity identity,
        [FromServices] AgentReleaseAssets release, CancellationToken ct)
    {
        var artifacts = release.GetChecksums();
        if (artifacts is null)
            return StatusCode(503, new { error = "Agent installer packages are not available in this build." });
        var result = await agents.InviteAsync(new(platformId, nodeId), request.Replace, ct);
        if (!result.IsSuccess)
            return result.ErrorType == ErrorType.Conflict ? Conflict(new { error = result.Error }) : NotFound(new { error = result.Error });
        await audit.LogAsync(AuditHelper.CreateLog(AuditAction.PlatformAgentInvitationIssued, AuditArea.Platform,
            user.UserName, user.UserId, platformId, new { nodeId, request.Replace }), ct);
        var invitation = result.Value!;
        return Ok(new AgentPairingResponse(invitation.Ticket, identity.Pin, invitation.ExpiresAt, artifacts));
    }

    [HttpDelete]
    [SwaggerOperation(OperationId = "PlatformAgentRevoke")]
    [RequirePermissionKey(PlatformPermissions.ManageAgentsKey)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Revoke(Guid platformId, [FromQuery] Guid? nodeId, CancellationToken ct)
    {
        if (!await agents.RevokeAsync(new(platformId, nodeId), ct)) return NotFound();
        await audit.LogAsync(AuditHelper.CreateLog(AuditAction.PlatformAgentRevoked, AuditArea.Platform,
            user.UserName, user.UserId, platformId, new { nodeId }), ct);
        return NoContent();
    }

    [HttpGet("release")]
    [SwaggerOperation(OperationId = "PlatformAgentRelease")]
    [RequirePermissionKey(PlatformPermissions.ManageAgentsKey)]
    [ProducesResponseType<AgentReleaseResponse>(200)]
    [ProducesResponseType(503)]
    public async Task<ActionResult<AgentReleaseResponse>> Release(Guid platformId, [FromQuery] Guid? nodeId,
        [FromServices] AgentReleaseAssets release, CancellationToken ct)
    {
        var status = await agents.StatusAsync(new(platformId, nodeId), ct);
        if (status?.FirstSeenAt is null) return NotFound(new { error = "Enrolled agent not found." });
        var hashes = release.GetChecksums();
        var version = release.GetAgentVersion();
        if (hashes == null || version == null) return StatusCode(503, new { error = "Agent release metadata is unavailable." });
        return Ok(new AgentReleaseResponse(version, hashes));
    }

    [HttpGet("history")]
    [SwaggerOperation(OperationId = "PlatformAgentHistory")]
    [RequirePermissionKey(PlatformPermissions.ReadKey)]
    [ProducesResponseType<AgentHistory>(200)]
    public async Task<ActionResult<AgentHistory>> History(Guid platformId, [FromQuery] Guid? nodeId,
        CancellationToken ct, [FromQuery] int hours = 24)
    {
        if (hours is not (1 or 24 or 168 or 720)) return BadRequest(new { error = "Select a 1, 24, 168 or 720 hour window." });
        var history = await agents.HistoryAsync(new(platformId, nodeId), hours, ct);
        return history == null ? NotFound() : Ok(history);
    }

    [HttpPost("check-updates")]
    [SwaggerOperation(OperationId = "PlatformAgentCheckUpdates")]
    [RequirePermissionKey(PlatformPermissions.CheckUpdatesKey)]
    [ProducesResponseType<AgentCheckJob>(200)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<AgentCheckJob>> CheckUpdates(Guid platformId, [FromQuery] Guid? nodeId, CancellationToken ct)
    {
        var result = await agents.RequestCheckAsync(new(platformId, nodeId), ct);
        if (!result.IsSuccess)
            return result.ErrorType == ErrorType.Conflict ? Conflict(new { error = result.Error }) : NotFound();
        await audit.LogAsync(AuditHelper.CreateLog(AuditAction.PlatformAgentAssessmentRequested, AuditArea.Platform,
            user.UserName, user.UserId, platformId, new { nodeId, jobId = result.Value!.Id }), ct);
        return Ok(result.Value);
    }
}
