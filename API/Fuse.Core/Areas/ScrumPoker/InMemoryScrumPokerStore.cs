using System.Security.Cryptography;
using System.Text;
using Fuse.Core.Helpers;

namespace Fuse.Core.Areas.ScrumPoker;

public sealed class InMemoryScrumPokerStore : IScrumPokerStore
{
    public const int MaxParticipantsPerRoom = 20;
    public const int MaxDisplayNameLength = 50;
    public static readonly TimeSpan ParticipantTimeout = TimeSpan.FromSeconds(10);

    private const int RoomCodeLength = 8;
    private const int ParticipantTokenLength = 32;
    private const string RoomCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly object _roomsLock = new();
    private readonly Dictionary<string, RoomState> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _roomLifetime;

    public InMemoryScrumPokerStore(TimeSpan? roomLifetime = null)
    {
        _roomLifetime = roomLifetime ?? TimeSpan.FromHours(4);
        if (_roomLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(roomLifetime), "Room lifetime must be greater than zero.");
    }

    public Result<ScrumPokerSession> CreateRoom(string displayName, DateTime utcNow, string? avatarColor = null)
    {
        var nameResult = ValidateDisplayName(displayName);
        if (!nameResult.IsSuccess)
            return Result<ScrumPokerSession>.Failure(nameResult.Error!, nameResult);
        var avatarResult = ValidateAvatarColor(avatarColor);
        if (!avatarResult.IsSuccess)
            return Result<ScrumPokerSession>.Failure(avatarResult.Error!, avatarResult);

        RoomState state;
        lock (_roomsLock)
        {
            string roomCode;
            do
            {
                roomCode = RandomString(RoomCodeAlphabet, RoomCodeLength);
            } while (_rooms.ContainsKey(roomCode));

            var participant = CreateParticipant(nameResult.Value!, utcNow, avatarColor);
            state = new RoomState(roomCode, utcNow, participant);
            _rooms.Add(roomCode, state);
        }

        return Result<ScrumPokerSession>.Success(CreateSession(state));
    }

    public Result<ScrumPokerSession> JoinRoom(string roomCode, string displayName, DateTime utcNow, string? participantToken = null, string? avatarColor = null)
    {
        var nameResult = ValidateDisplayName(displayName);
        if (!nameResult.IsSuccess)
            return Result<ScrumPokerSession>.Failure(nameResult.Error!, nameResult);
        var avatarResult = ValidateAvatarColor(avatarColor);
        if (!avatarResult.IsSuccess)
            return Result<ScrumPokerSession>.Failure(avatarResult.Error!, avatarResult);

        var state = FindActiveRoom(roomCode, utcNow);
        if (state is null)
            return Result<ScrumPokerSession>.Failure("Room was not found or has expired.", ErrorType.NotFound);

        lock (state.Gate)
        {
            var participant = state.KnownParticipants.Values.FirstOrDefault(p =>
                participantToken is not null && FixedTimeEquals(p.Token, participantToken));
            if (participant is not null && state.Participants.ContainsKey(participant.Id))
            {
                participant = participant with
                {
                    DisplayName = nameResult.Value!,
                    AvatarColor = avatarColor ?? participant.AvatarColor,
                    LastSeenUtc = utcNow
                };
                state.Participants[participant.Id] = participant;
                state.KnownParticipants[participant.Id] = participant;
                state.LastActivityUtc = utcNow;
                return Result<ScrumPokerSession>.Success(CreateSession(state, participant));
            }

            if (state.Participants.Count >= MaxParticipantsPerRoom)
                return Result<ScrumPokerSession>.Failure("The room is full.", ErrorType.Conflict);

            participant = participant is null
                ? CreateParticipant(nameResult.Value!, utcNow, avatarColor)
                : participant with
                {
                    DisplayName = nameResult.Value!,
                    AvatarColor = avatarColor ?? participant.AvatarColor,
                    LastSeenUtc = utcNow
                };
            state.Participants.Add(participant.Id, participant);
            state.KnownParticipants[participant.Id] = participant;
            state.LastActivityUtc = utcNow;
            state.Revision++;
            return Result<ScrumPokerSession>.Success(CreateSession(state, participant));
        }
    }

    public Result<ScrumPokerSession> JoinOrCreateRoom(string roomCode, string displayName, DateTime utcNow, string? participantToken = null, string? avatarColor = null)
    {
        var nameResult = ValidateDisplayName(displayName);
        if (!nameResult.IsSuccess)
            return Result<ScrumPokerSession>.Failure(nameResult.Error!, nameResult);

        var normalizedCode = NormalizeRoomCode(roomCode);
        if (normalizedCode is null)
            return Result<ScrumPokerSession>.Failure("The room code is invalid.");

        lock (_roomsLock)
        {
            if (_rooms.TryGetValue(normalizedCode, out var existingRoom))
            {
                lock (existingRoom.Gate)
                {
                    if (!IsExpired(existingRoom, utcNow))
                        return JoinRoom(normalizedCode, nameResult.Value!, utcNow, participantToken, avatarColor);
                }

                _rooms.Remove(normalizedCode);
            }

            var participant = CreateParticipant(nameResult.Value!, utcNow, avatarColor);
            var state = new RoomState(normalizedCode, utcNow, participant);
            _rooms.Add(normalizedCode, state);
            return Result<ScrumPokerSession>.Success(CreateSession(state));
        }
    }

    public bool RoomExists(string roomCode, DateTime utcNow) => FindActiveRoom(roomCode, utcNow) is not null;

    public Result<ScrumPokerRoom> GetRoom(string roomCode, string participantToken, DateTime utcNow)
    {
        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            // Update caller first so they are never evicted by their own poll.
            state.Participants[participant.Id] = participant with { LastSeenUtc = utcNow };
            state.LastActivityUtc = utcNow;
            EvictStaleParticipants(state, utcNow);
            if (state.AutoReveal && state.Phase == ScrumPokerPhase.Voting && HasEveryoneVoted(state))
            {
                state.Phase = ScrumPokerPhase.Revealed;
                state.Revision++;
            }
            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    public Result<ScrumPokerRoom> SelectCard(string roomCode, string participantToken, ScrumPokerCard? card, DateTime utcNow)
    {
        if (card is not null && !Enum.IsDefined(card.Value))
            return Result<ScrumPokerRoom>.Failure("The selected card is not valid.");

        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            if (state.Phase == ScrumPokerPhase.Revealed && state.LockVotesAfterReveal)
                return Result<ScrumPokerRoom>.Failure("The cards have already been revealed. Reset the room to vote again.", ErrorType.Conflict);

            state.Participants[participant.Id] = participant with { SelectedCard = card, LastSeenUtc = utcNow };
            state.LastActivityUtc = utcNow;
            state.Revision++;

            if (state.AutoReveal && HasEveryoneVoted(state))
            {
                state.Phase = ScrumPokerPhase.Revealed;
                state.Revision++;
            }

            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    public Result<ScrumPokerRoom> SetAutoReveal(string roomCode, string participantToken, bool enabled, DateTime utcNow)
    {
        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            state.Participants[participant.Id] = participant with { LastSeenUtc = utcNow };
            state.LastActivityUtc = utcNow;

            var changed = state.AutoReveal != enabled;
            if (changed)
            {
                state.AutoReveal = enabled;
                state.Revision++;
            }

            if (state.AutoReveal && state.Phase == ScrumPokerPhase.Voting && HasEveryoneVoted(state))
            {
                state.Phase = ScrumPokerPhase.Revealed;
                state.Revision++;
            }

            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    public Result<ScrumPokerRoom> Reveal(string roomCode, string participantToken, DateTime utcNow)
    {
        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            state.Participants[participant.Id] = participant with { LastSeenUtc = utcNow };
            state.LastActivityUtc = utcNow;
            if (state.Phase == ScrumPokerPhase.Voting)
            {
                state.Phase = ScrumPokerPhase.Revealed;
                state.Revision++;
            }

            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    public Result<ScrumPokerRoom> SetLockVotesAfterReveal(string roomCode, string participantToken, bool enabled, DateTime utcNow)
    {
        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            state.Participants[participant.Id] = participant with { LastSeenUtc = utcNow };
            state.LastActivityUtc = utcNow;

            if (state.LockVotesAfterReveal != enabled)
            {
                state.LockVotesAfterReveal = enabled;
                state.Revision++;
            }

            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    public Result<ScrumPokerRoom> Hide(string roomCode, string participantToken, DateTime utcNow)
    {
        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            state.Participants[participant.Id] = participant with { LastSeenUtc = utcNow };
            state.LastActivityUtc = utcNow;
            if (state.Phase == ScrumPokerPhase.Revealed)
            {
                state.Phase = ScrumPokerPhase.Voting;
                state.Revision++;
            }

            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    public Result<ScrumPokerRoom> Reset(string roomCode, string participantToken, DateTime utcNow)
    {
        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            foreach (var id in state.Participants.Keys.ToArray())
            {
                var current = state.Participants[id];
                var resetParticipant = current with
                {
                    SelectedCard = null,
                    LastSeenUtc = id == participant.Id ? utcNow : current.LastSeenUtc
                };
                state.Participants[id] = resetParticipant;
                state.KnownParticipants[id] = resetParticipant;
            }

            foreach (var id in state.KnownParticipants.Keys.ToArray())
                state.KnownParticipants[id] = state.KnownParticipants[id] with { SelectedCard = null };

            state.Phase = ScrumPokerPhase.Voting;
            state.Round++;
            state.LastActivityUtc = utcNow;
            state.Revision++;
            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    public Result<ScrumPokerRoom> Leave(string roomCode, string participantToken, DateTime utcNow)
    {
        var stateResult = GetParticipantRoom(roomCode, participantToken, utcNow);
        if (!stateResult.IsSuccess)
            return Result<ScrumPokerRoom>.Failure(stateResult.Error!, stateResult);

        var (state, participant) = stateResult.Value!;
        lock (state.Gate)
        {
            state.Participants.Remove(participant.Id);
            state.KnownParticipants[participant.Id] = participant;
            state.LastActivityUtc = utcNow;
            state.Revision++;
            return Result<ScrumPokerRoom>.Success(CreateRoomSnapshot(state));
        }
    }

    private Result<string> ValidateDisplayName(string displayName)
    {
        var normalized = displayName?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? Result<string>.Failure("A display name is required.")
            : normalized.Length > MaxDisplayNameLength
                ? Result<string>.Failure($"Display names must be {MaxDisplayNameLength} characters or fewer.")
                : Result<string>.Success(normalized);
    }

    private static Result<string?> ValidateAvatarColor(string? avatarColor)
    {
        if (avatarColor is null)
            return Result<string?>.Success(null);

        var normalized = avatarColor.Trim().ToLowerInvariant();
        return normalized.StartsWith("avatar-image-") &&
               int.TryParse(normalized[13..], out var imageNumber) && imageNumber is >= 1 and <= 18 ||
               normalized.Length == 7 && normalized[0] == '#' && normalized[1..].All(Uri.IsHexDigit)
            ? Result<string?>.Success(normalized)
            : Result<string?>.Failure("The avatar color is invalid.");
    }

    private static string? NormalizeRoomCode(string roomCode)
    {
        var normalized = roomCode?.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(normalized) || normalized.Length > 20 || normalized.Any(char.IsWhiteSpace)
            ? null
            : normalized;
    }

    private RoomState? FindActiveRoom(string roomCode, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
            return null;

        lock (_roomsLock)
        {
            if (!_rooms.TryGetValue(roomCode.Trim(), out var state))
                return null;

            lock (state.Gate)
            {
                if (IsExpired(state, utcNow))
                {
                    _rooms.Remove(state.RoomCode);
                    return null;
                }

                return state;
            }
        }
    }

    private bool IsExpired(RoomState state, DateTime utcNow)
    {
        var lastParticipantActivity = state.Participants.Values
            .Select(participant => participant.LastSeenUtc)
            .DefaultIfEmpty(state.LastActivityUtc)
            .Max();
        return utcNow - lastParticipantActivity >= _roomLifetime;
    }

    private Result<(RoomState State, ScrumPokerParticipant Participant)> GetParticipantRoom(string roomCode, string token, DateTime utcNow)
    {
        var state = FindActiveRoom(roomCode, utcNow);
        if (state is null)
            return Result<(RoomState, ScrumPokerParticipant)>.Failure("Room was not found or has expired.", ErrorType.NotFound);

        lock (state.Gate)
        {
            var participant = state.Participants.Values.FirstOrDefault(p => FixedTimeEquals(p.Token, token));
            return participant is null
                ? Result<(RoomState, ScrumPokerParticipant)>.Failure("The participant token is invalid.", ErrorType.Unauthorized)
                : Result<(RoomState, ScrumPokerParticipant)>.Success((state, participant));
        }
    }

    private static ScrumPokerParticipant CreateParticipant(string displayName, DateTime utcNow, string? avatarColor) =>
        new(Guid.NewGuid(), displayName, RandomString("", ParticipantTokenLength), avatarColor?.Trim().ToLowerInvariant(), null, utcNow);

    private static ScrumPokerSession CreateSession(RoomState state, ScrumPokerParticipant? participant = null)
    {
        var selectedParticipant = participant ?? state.Participants.Values.First();
        return new(CreateRoomSnapshot(state), selectedParticipant);
    }

    private static ScrumPokerRoom CreateRoomSnapshot(RoomState state) =>
        new(state.RoomCode, state.Round, state.Phase, state.AutoReveal, state.LockVotesAfterReveal, state.Revision, state.CreatedUtc, state.LastActivityUtc, state.Participants.Values.ToArray());

    private static void EvictStaleParticipants(RoomState state, DateTime utcNow)
    {
        var staleIds = state.Participants
            .Where(kvp => utcNow - kvp.Value.LastSeenUtc >= ParticipantTimeout)
            .Select(kvp => kvp.Key)
            .ToList();

        if (staleIds.Count == 0)
            return;

        foreach (var id in staleIds)
            state.Participants.Remove(id);

        state.LastActivityUtc = utcNow;
        state.Revision++;
    }

    private static bool HasEveryoneVoted(RoomState state) =>
        state.Participants.Count > 0 && state.Participants.Values.All(participant => participant.SelectedCard is not null);

    private static string RandomString(string alphabet, int length)
    {
        if (string.IsNullOrEmpty(alphabet))
        {
            var bytes = RandomNumberGenerator.GetBytes(length);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        var result = new StringBuilder(length);
        var buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        foreach (var value in buffer)
            result.Append(alphabet[value % alphabet.Length]);
        return result.ToString();
    }

    private static bool FixedTimeEquals(string expected, string? actual)
    {
        if (string.IsNullOrEmpty(actual))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private sealed class RoomState(string roomCode, DateTime createdUtc, ScrumPokerParticipant participant)
    {
        public object Gate { get; } = new();
        public string RoomCode { get; } = roomCode;
        public DateTime CreatedUtc { get; } = createdUtc;
        public DateTime LastActivityUtc { get; set; } = createdUtc;
        public int Round { get; set; } = 1;
        public ScrumPokerPhase Phase { get; set; } = ScrumPokerPhase.Voting;
        public bool AutoReveal { get; set; } = false;
        public bool LockVotesAfterReveal { get; set; } = false;
        public long Revision { get; set; } = 1;
        public Dictionary<Guid, ScrumPokerParticipant> Participants { get; } = new() { [participant.Id] = participant };
        public Dictionary<Guid, ScrumPokerParticipant> KnownParticipants { get; } = new() { [participant.Id] = participant };
    }
}
