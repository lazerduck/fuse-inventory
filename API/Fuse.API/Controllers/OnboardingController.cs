using Fuse.API.CurrentUser;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OnboardingController(IFuseStore fuseStore, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("progress")]
    [ProducesResponseType<UserGuideProgress>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserGuideProgress>> GetProgress(CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            return Unauthorized();

        var progress = await fuseStore.GetAsync(
            snapshot => snapshot.SecurityContext.GuideProgress.GetValueOrDefault(userId), ct);

        return Ok(progress ?? EmptyProgress());
    }

    [HttpPut("progress")]
    [ProducesResponseType<UserGuideProgress>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserGuideProgress>> UpdateProgress(
        [FromBody] UpdateGuideProgress request,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            return Unauthorized();

        var completedStepIds = (request.CompletedStepIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (completedStepIds.Any(id => id.Length > 100) || request.ActiveGuideId?.Length > 100)
            return BadRequest(new { error = "Guide and step identifiers must be 100 characters or fewer." });

        var progress = new UserGuideProgress(
            completedStepIds,
            string.IsNullOrWhiteSpace(request.ActiveGuideId) ? null : request.ActiveGuideId.Trim(),
            request.HasCompletedGettingStarted,
            request.LastCompletedAt,
            DateTime.UtcNow);

        await fuseStore.UpdateAsync(snapshot => snapshot with
        {
            SecurityContext = snapshot.SecurityContext with
            {
                GuideProgress = new Dictionary<Guid, UserGuideProgress>(snapshot.SecurityContext.GuideProgress)
                {
                    [userId] = progress
                }
            }
        }, ct);

        return Ok(progress);
    }

    private static UserGuideProgress EmptyProgress() =>
        new(Array.Empty<string>(), "first-application", false, null, DateTime.UtcNow);
}

public sealed record UpdateGuideProgress(
    IReadOnlyList<string>? CompletedStepIds,
    string? ActiveGuideId,
    bool HasCompletedGettingStarted,
    DateTime? LastCompletedAt);
