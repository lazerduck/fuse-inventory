namespace Fuse.Core.Areas.ScrumPoker;

public enum ScrumPokerCard
{
    Zero,
    Half,
    One,
    Two,
    Three,
    Five,
    Eight,
    Thirteen,
    Twenty,
    Forty,
    Hundred,
    Question,
    Coffee
}

public enum ScrumPokerPhase
{
    Voting,
    Revealed
}

public sealed record ScrumPokerParticipant(
    Guid Id,
    string DisplayName,
    string Token,
    string? AvatarColor,
    ScrumPokerCard? SelectedCard,
    DateTime LastSeenUtc);

public sealed record ScrumPokerRoom(
    string RoomCode,
    Guid OwnerParticipantId,
    string OwnerToken,
    Guid? CurrentHostParticipantId,
    int Round,
    ScrumPokerPhase Phase,
    bool AutoReveal,
    bool LockVotesAfterReveal,
    long Revision,
    DateTime CreatedUtc,
    DateTime LastActivityUtc,
    IReadOnlyList<ScrumPokerParticipant> Participants);

public sealed record ScrumPokerSession(
    ScrumPokerRoom Room,
    ScrumPokerParticipant Participant,
    string? OwnerToken = null);
