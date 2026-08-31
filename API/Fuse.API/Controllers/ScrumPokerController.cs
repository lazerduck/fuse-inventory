using Fuse.Core.Areas.ScrumPoker;
using Fuse.Core.Interfaces;
using Fuse.Core.Helpers;
using Microsoft.AspNetCore.Mvc;
using FuseResult = Fuse.Core.Helpers.IResult;

namespace Fuse.API.Controllers;

[ApiController]
[Route("api/scrum-poker")]
[AllowWithoutPermission]
public sealed class ScrumPokerController(
    IScrumPokerStore store,
    IFuseStore fuseStore) : ControllerBase
{
    [HttpPost("rooms")]
    [ProducesResponseType<ScrumPokerSessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerSessionResponse>> CreateRoom([FromBody] ScrumPokerJoinRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.CreateRoom(request.DisplayName, DateTime.UtcNow, request.AvatarColor);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, ToSessionResponse(result.Value!))
            : ToError<ScrumPokerSessionResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/join")]
    [ProducesResponseType<ScrumPokerSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScrumPokerSessionResponse>> JoinRoom(string roomCode, [FromBody] ScrumPokerJoinRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.JoinRoom(roomCode, request.DisplayName, DateTime.UtcNow, request.ParticipantToken, request.AvatarColor, allowRemovedParticipantAsNew: true, ownerToken: request.OwnerToken);
        return result.IsSuccess ? Ok(ToSessionResponse(result.Value!)) : ToError<ScrumPokerSessionResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/enter")]
    [ProducesResponseType<ScrumPokerSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScrumPokerSessionResponse>> EnterRoom(string roomCode, [FromBody] ScrumPokerJoinRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.JoinOrCreateRoom(roomCode, request.DisplayName, DateTime.UtcNow, request.ParticipantToken, request.AvatarColor, request.OwnerToken);
        return result.IsSuccess ? Ok(ToSessionResponse(result.Value!)) : ToError<ScrumPokerSessionResponse>(result);
    }

    [HttpGet("rooms/{roomCode}/availability")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetRoomAvailability(string roomCode)
    {
        if (!await IsEnabled())
            return NotFound();

        return Ok(new { exists = store.RoomExists(roomCode, DateTime.UtcNow) });
    }

    [HttpGet("rooms/{roomCode}/state")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> GetState(string roomCode, [FromQuery] string participantToken)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.GetRoom(roomCode, participantToken, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, participantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPut("rooms/{roomCode}/card")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> SelectCard(string roomCode, [FromBody] ScrumPokerCardRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.SelectCard(roomCode, request.ParticipantToken, request.Card, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.ParticipantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/settings/auto-reveal")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> SetAutoReveal(string roomCode, [FromBody] ScrumPokerAutoRevealRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.SetAutoReveal(roomCode, request.ParticipantToken, request.Enabled, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.ParticipantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/settings/lock-votes-after-reveal")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> SetLockVotesAfterReveal(string roomCode, [FromBody] ScrumPokerLockVotesAfterRevealRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.SetLockVotesAfterReveal(roomCode, request.ParticipantToken, request.Enabled, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.ParticipantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/reveal")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> Reveal(string roomCode, [FromBody] ScrumPokerParticipantRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.Reveal(roomCode, request.ParticipantToken, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.ParticipantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/reset")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> Reset(string roomCode, [FromBody] ScrumPokerParticipantRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.Reset(roomCode, request.ParticipantToken, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.ParticipantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/hide")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> Hide(string roomCode, [FromBody] ScrumPokerParticipantRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.Hide(roomCode, request.ParticipantToken, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.ParticipantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Leave(string roomCode, [FromBody] ScrumPokerParticipantRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.Leave(roomCode, request.ParticipantToken, DateTime.UtcNow);
        return result.IsSuccess ? NoContent() : ToErrorResult(result);
    }

    [HttpPost("rooms/{roomCode}/remove-participant")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> RemoveParticipant(string roomCode, [FromBody] ScrumPokerRemoveParticipantRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.RemoveParticipant(roomCode, request.OwnerToken, request.ParticipantId, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.OwnerToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/transfer-ownership")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> TransferOwnership(string roomCode, [FromBody] ScrumPokerTransferOwnershipRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.TransferOwnership(roomCode, request.OwnerToken, request.ParticipantId, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.OwnerToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    [HttpPost("rooms/{roomCode}/transfer-host")]
    [ProducesResponseType<ScrumPokerRoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrumPokerRoomResponse>> TransferHost(string roomCode, [FromBody] ScrumPokerTransferHostRequest request)
    {
        if (!await IsEnabled())
            return NotFound();

        var result = store.TransferHost(roomCode, request.ParticipantToken, request.ParticipantId, DateTime.UtcNow);
        return result.IsSuccess ? Ok(ToRoomResponse(result.Value!, request.ParticipantToken)) : ToError<ScrumPokerRoomResponse>(result);
    }

    private Task<bool> IsEnabled() => fuseStore.GetAsync(snapshot => snapshot.AppSettings.ScrumPokerEnabled);

    private static ScrumPokerSessionResponse ToSessionResponse(ScrumPokerSession session) =>
        new(session.Room.RoomCode, session.Participant.Id, session.Participant.Token,
            session.Participant.Id == session.Room.OwnerParticipantId ? session.OwnerToken : null,
            ToRoomResponse(session.Room, session.Participant.Token));

    private static ScrumPokerRoomResponse ToRoomResponse(ScrumPokerRoom room, string participantToken) =>
        new(
            room.RoomCode,
            room.OwnerParticipantId,
            room.CurrentHostParticipantId,
            room.Round,
            room.Phase,
            room.AutoReveal,
            room.LockVotesAfterReveal,
            room.Revision,
            room.CreatedUtc,
            room.LastActivityUtc,
            room.Phase == ScrumPokerPhase.Revealed ? CalculateAverage(room) : null,
            room.Participants.Select(participant => new ScrumPokerParticipantResponse(
                participant.Id,
                participant.DisplayName,
                participant.AvatarColor,
                participant.SelectedCard is not null,
                room.Phase == ScrumPokerPhase.Revealed || FixedTimeEquals(participant.Token, participantToken)
                    ? participant.SelectedCard
                    : null)).ToArray());

    private static double? CalculateAverage(ScrumPokerRoom room)
    {
        var values = room.Participants
            .Select(participant => participant.SelectedCard switch
            {
                ScrumPokerCard.Zero => 0d,
                ScrumPokerCard.Half => 0.5d,
                ScrumPokerCard.One => 1d,
                ScrumPokerCard.Two => 2d,
                ScrumPokerCard.Three => 3d,
                ScrumPokerCard.Five => 5d,
                ScrumPokerCard.Eight => 8d,
                ScrumPokerCard.Thirteen => 13d,
                ScrumPokerCard.Twenty => 20d,
                ScrumPokerCard.Forty => 40d,
                ScrumPokerCard.Hundred => 100d,
                _ => (double?)null
            })
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return values.Length == 0 ? null : Math.Round(values.Average(), 2);
    }

    private static ActionResult<T> ToError<T>(FuseResult result) => result.ErrorType switch
    {
        ErrorType.NotFound => new NotFoundObjectResult(new { error = result.Error }),
        ErrorType.Unauthorized => new UnauthorizedObjectResult(new { error = result.Error }),
        ErrorType.Conflict => new ConflictObjectResult(new { error = result.Error }),
        _ => new BadRequestObjectResult(new { error = result.Error })
    };

    private static IActionResult ToErrorResult(FuseResult result) => result.ErrorType switch
    {
        ErrorType.NotFound => new NotFoundObjectResult(new { error = result.Error }),
        ErrorType.Unauthorized => new UnauthorizedObjectResult(new { error = result.Error }),
        ErrorType.Conflict => new ConflictObjectResult(new { error = result.Error }),
        _ => new BadRequestObjectResult(new { error = result.Error })
    };

    private static bool FixedTimeEquals(string expected, string? actual)
    {
        if (string.IsNullOrEmpty(actual))
            return false;

        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
        var actualBytes = System.Text.Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length &&
               System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}

public sealed record ScrumPokerJoinRequest(string DisplayName, string? ParticipantToken = null, string? AvatarColor = null, string? OwnerToken = null);

public sealed record ScrumPokerTransferOwnershipRequest(string OwnerToken, Guid ParticipantId);

public sealed record ScrumPokerTransferHostRequest(string ParticipantToken, Guid ParticipantId);

public sealed record ScrumPokerParticipantRequest(string ParticipantToken);

public sealed record ScrumPokerRemoveParticipantRequest(string OwnerToken, Guid ParticipantId);

public sealed record ScrumPokerCardRequest(string ParticipantToken, ScrumPokerCard? Card);

public sealed record ScrumPokerAutoRevealRequest(string ParticipantToken, bool Enabled);

public sealed record ScrumPokerLockVotesAfterRevealRequest(string ParticipantToken, bool Enabled);

public sealed record ScrumPokerSessionResponse(
    string RoomCode,
    Guid ParticipantId,
    string ParticipantToken,
    string? OwnerToken,
    ScrumPokerRoomResponse Room);

public sealed record ScrumPokerRoomResponse(
    string RoomCode,
    Guid OwnerParticipantId,
    Guid? CurrentHostParticipantId,
    int Round,
    ScrumPokerPhase Phase,
    bool AutoReveal,
    bool LockVotesAfterReveal,
    long Revision,
    DateTime CreatedUtc,
    DateTime LastActivityUtc,
    double? Average,
    IReadOnlyList<ScrumPokerParticipantResponse> Participants);

public sealed record ScrumPokerParticipantResponse(
    Guid Id,
    string DisplayName,
    string? AvatarColor,
    bool HasVoted,
    ScrumPokerCard? Card);
