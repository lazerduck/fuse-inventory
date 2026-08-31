using Fuse.Core.Helpers;

namespace Fuse.Core.Areas.ScrumPoker;

public interface IScrumPokerStore
{
    Result<ScrumPokerSession> CreateRoom(string displayName, DateTime utcNow, string? avatarColor = null);
    Result<ScrumPokerSession> JoinRoom(string roomCode, string displayName, DateTime utcNow, string? participantToken = null, string? avatarColor = null, bool allowRemovedParticipantAsNew = false, string? ownerToken = null);
    Result<ScrumPokerSession> JoinOrCreateRoom(string roomCode, string displayName, DateTime utcNow, string? participantToken = null, string? avatarColor = null, string? ownerToken = null);
    bool RoomExists(string roomCode, DateTime utcNow);
    Result<ScrumPokerRoom> GetRoom(string roomCode, string participantToken, DateTime utcNow);
    Result<ScrumPokerRoom> SelectCard(string roomCode, string participantToken, ScrumPokerCard? card, DateTime utcNow);
    Result<ScrumPokerRoom> SetAutoReveal(string roomCode, string participantToken, bool enabled, DateTime utcNow);
    Result<ScrumPokerRoom> SetLockVotesAfterReveal(string roomCode, string participantToken, bool enabled, DateTime utcNow);
    Result<ScrumPokerRoom> Reveal(string roomCode, string participantToken, DateTime utcNow);
    Result<ScrumPokerRoom> Hide(string roomCode, string participantToken, DateTime utcNow);
    Result<ScrumPokerRoom> Reset(string roomCode, string participantToken, DateTime utcNow);
    Result<ScrumPokerRoom> Leave(string roomCode, string participantToken, DateTime utcNow);
    Result<ScrumPokerRoom> RemoveParticipant(string roomCode, string ownerToken, Guid participantId, DateTime utcNow);
    Result<ScrumPokerRoom> TransferOwnership(string roomCode, string ownerToken, Guid participantId, DateTime utcNow);
    Result<ScrumPokerRoom> TransferHost(string roomCode, string participantToken, Guid participantId, DateTime utcNow);
}
