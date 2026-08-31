using Fuse.Core.Areas.ScrumPoker;
using Fuse.Core.Helpers;
using Xunit;

namespace Fuse.Tests.Areas.ScrumPoker;

public sealed class InMemoryScrumPokerStoreTests
{
    [Fact]
    public void CreateRoom_CreatorIsOwnerAndHost()
    {
        var store = new InMemoryScrumPokerStore();
        var session = store.CreateRoom("Damian", DateTime.UtcNow).Value!;

        Assert.Equal(session.Participant.Id, session.Room.OwnerParticipantId);
        Assert.Equal(session.Participant.Id, session.Room.CurrentHostParticipantId);
        Assert.NotEmpty(session.OwnerToken);
    }

    [Fact]
    public void OwnerToken_CannotPromoteSecondParticipantWhileOwnerIsActive()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", DateTime.UtcNow).Value!;

        var guest = store.JoinRoom(
            owner.Room.RoomCode,
            "Guest",
            DateTime.UtcNow.AddSeconds(1),
            ownerToken: owner.OwnerToken).Value!;

        Assert.Equal(owner.Participant.Id, guest.Room.OwnerParticipantId);
        Assert.Equal(owner.Participant.Id, guest.Room.CurrentHostParticipantId);
        Assert.Null(guest.OwnerToken);
    }

    [Fact]
    public void AvatarImageIdentifier_IsAcceptedAndNormalized()
    {
        var store = new InMemoryScrumPokerStore();

        var session = store.CreateRoom("Damian", DateTime.UtcNow, "AVATAR-IMAGE-18").Value!;
        var invalid = store.JoinRoom(session.Room.RoomCode, "Sarah", DateTime.UtcNow.AddSeconds(1), avatarColor: "avatar-image-19");

        Assert.Equal("avatar-image-18", session.Participant.AvatarColor);
        Assert.False(invalid.IsSuccess);
        Assert.Equal("The avatar color is invalid.", invalid.Error);
    }

    [Fact]
    public void OwnerLeaves_TemporaryHostFacilitates_AndOwnerReturnsAsHost()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", DateTime.UtcNow).Value!;
        var sarah = store.JoinRoom(owner.Room.RoomCode, "Sarah", DateTime.UtcNow.AddSeconds(1)).Value!;
        var john = store.JoinRoom(owner.Room.RoomCode, "John", DateTime.UtcNow.AddSeconds(2)).Value!;

        var afterOwnerLeaves = store.Leave(owner.Room.RoomCode, owner.Participant.Token, DateTime.UtcNow.AddSeconds(3)).Value!;
        Assert.Equal(sarah.Participant.Id, afterOwnerLeaves.CurrentHostParticipantId);
        Assert.Equal(owner.Participant.Id, afterOwnerLeaves.OwnerParticipantId);
        Assert.Equal(sarah.Participant.Id, store.Reveal(owner.Room.RoomCode, sarah.Participant.Token, DateTime.UtcNow.AddSeconds(4)).Value!.CurrentHostParticipantId);

        var ownerReturns = store.JoinRoom(owner.Room.RoomCode, "Damian", DateTime.UtcNow.AddSeconds(5), owner.Participant.Token).Value!;
        Assert.Equal(owner.Participant.Id, ownerReturns.Participant.Id);
        Assert.Equal(owner.Participant.Id, ownerReturns.Room.CurrentHostParticipantId);
        Assert.Contains(ownerReturns.Room.Participants, participant => participant.Id == john.Participant.Id);
    }

    [Fact]
    public void EmptyRoom_ExpiresAfterFourHoursWithoutActivity()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", now).Value!;
        var empty = store.Leave(owner.Room.RoomCode, owner.Participant.Token, now.AddSeconds(1)).Value!;

        Assert.Empty(empty.Participants);
        Assert.Null(empty.CurrentHostParticipantId);
        Assert.True(store.RoomExists(owner.Room.RoomCode, now.AddHours(4).AddSeconds(-1)));
        Assert.False(store.RoomExists(owner.Room.RoomCode, now.AddHours(4).AddSeconds(1)));

        var rejoin = store.JoinRoom(owner.Room.RoomCode, "Sarah", now.AddHours(4).AddSeconds(2));
        Assert.False(rejoin.IsSuccess);
    }

    [Fact]
    public void JoinOrCreateRoom_RecreatesExpiredRoomWithSameCode()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var original = store.CreateRoom("Damian", now).Value!;
        store.Leave(original.Room.RoomCode, original.Participant.Token, now.AddSeconds(1));

        var recreated = store.JoinOrCreateRoom(original.Room.RoomCode, "Sarah", now.AddHours(4).AddSeconds(2), avatarColor: "#123456").Value!;

        Assert.Equal(original.Room.RoomCode, recreated.Room.RoomCode);
        Assert.Equal(Guid.Empty, recreated.Room.OwnerParticipantId);
        Assert.Equal(recreated.Participant.Id, recreated.Room.CurrentHostParticipantId);
        Assert.Equal(original.Room.OwnerToken, recreated.Room.OwnerToken);
        Assert.Null(recreated.OwnerToken);
        Assert.Equal("#123456", recreated.Participant.AvatarColor);

        store.Leave(recreated.Room.RoomCode, recreated.Participant.Token, now.AddHours(4).AddSeconds(3));
        var ownerReturns = store.JoinOrCreateRoom(
            original.Room.RoomCode,
            "Different name",
            now.AddHours(4).AddSeconds(4),
            ownerToken: original.Room.OwnerToken).Value!;

        Assert.Equal(ownerReturns.Participant.Id, ownerReturns.Room.OwnerParticipantId);
        Assert.Equal(ownerReturns.Participant.Id, ownerReturns.Room.CurrentHostParticipantId);
        Assert.Equal(original.Room.OwnerToken, ownerReturns.OwnerToken);
    }

    [Fact]
    public void JoinOrCreateRoom_SecondParticipantJoinsRoomRecreatedFromExpiredCode()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var original = store.CreateRoom("Damian", now).Value!;
        store.Leave(original.Room.RoomCode, original.Participant.Token, now.AddSeconds(1));

        var first = store.JoinOrCreateRoom(
            original.Room.RoomCode,
            "Sarah",
            now.AddHours(4).AddSeconds(2)).Value!;
        var second = store.JoinOrCreateRoom(
            original.Room.RoomCode,
            "Taylor",
            now.AddHours(4).AddSeconds(3)).Value!;

        Assert.Equal(original.Room.RoomCode, first.Room.RoomCode);
        Assert.Equal(original.Room.RoomCode, second.Room.RoomCode);
        Assert.Equal(first.Room.CreatedUtc, second.Room.CreatedUtc);
        Assert.Contains(second.Room.Participants, participant => participant.Id == first.Participant.Id);
        Assert.Contains(second.Room.Participants, participant => participant.Id == second.Participant.Id);
    }

    [Fact]
    public void ActiveParticipantActivityExtendsRoomLifetime()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", now).Value!;

        Assert.True(store.RoomExists(owner.Room.RoomCode, now.AddHours(3).AddMinutes(59)));
        Assert.True(store.GetRoom(owner.Room.RoomCode, owner.Participant.Token, now.AddHours(3).AddMinutes(59)).IsSuccess);
        Assert.True(store.RoomExists(owner.Room.RoomCode, now.AddHours(7).AddMinutes(58)));
        Assert.False(store.RoomExists(owner.Room.RoomCode, now.AddHours(7).AddMinutes(59).AddSeconds(1)));
    }

    [Fact]
    public void TransferOwnership_RequiresOwner_AndMakesNewOwnerHost()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", DateTime.UtcNow).Value!;
        var sarah = store.JoinRoom(owner.Room.RoomCode, "Sarah", DateTime.UtcNow.AddSeconds(1)).Value!;

        var unauthorized = store.TransferOwnership(owner.Room.RoomCode, sarah.Participant.Token, owner.Participant.Id, DateTime.UtcNow.AddSeconds(2));
        Assert.False(unauthorized.IsSuccess);

        var transferred = store.TransferOwnership(owner.Room.RoomCode, owner.Room.OwnerToken, sarah.Participant.Id, DateTime.UtcNow.AddSeconds(3)).Value!;
        Assert.Equal(sarah.Participant.Id, transferred.OwnerParticipantId);
        Assert.Equal(sarah.Participant.Id, transferred.CurrentHostParticipantId);
        Assert.NotEqual(owner.Room.OwnerToken, transferred.OwnerToken);
        Assert.False(store.TransferOwnership(owner.Room.RoomCode, owner.Room.OwnerToken, owner.Participant.Id, DateTime.UtcNow.AddSeconds(4)).IsSuccess);
    }

    [Fact]
    public void Reveal_RequiresCurrentHost_NotPermanentOwner()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", DateTime.UtcNow).Value!;
        var sarah = store.JoinRoom(owner.Room.RoomCode, "Sarah", DateTime.UtcNow.AddSeconds(1)).Value!;
        store.Leave(owner.Room.RoomCode, owner.Participant.Token, DateTime.UtcNow.AddSeconds(2));

        Assert.False(store.Reveal(owner.Room.RoomCode, owner.Participant.Token, DateTime.UtcNow.AddSeconds(3)).IsSuccess);
        Assert.True(store.Reveal(owner.Room.RoomCode, sarah.Participant.Token, DateTime.UtcNow.AddSeconds(4)).IsSuccess);
    }

    [Fact]
    public void SelectCard_AfterReveal_IsAllowedUnlessVotesAreLocked()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", DateTime.UtcNow).Value!;
        var now = DateTime.UtcNow.AddSeconds(1);

        store.Reveal(owner.Room.RoomCode, owner.Participant.Token, now);

        var changedVote = store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, ScrumPokerCard.Five, now.AddSeconds(1));
        Assert.True(changedVote.IsSuccess);

        store.SetLockVotesAfterReveal(owner.Room.RoomCode, owner.Participant.Token, true, now.AddSeconds(2));

        var lockedVote = store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, ScrumPokerCard.Eight, now.AddSeconds(3));
        Assert.False(lockedVote.IsSuccess);
        Assert.Equal(ErrorType.Conflict, lockedVote.ErrorType);
    }

    [Fact]
public void ExpiredRoomMetadata_IsForgottenAfterSixtyDays()
        {
            var now = DateTime.UtcNow;
            var store = new InMemoryScrumPokerStore();
            var original = store.CreateRoom("Damian", now).Value!;

            store.RoomExists(original.Room.RoomCode, now.AddHours(4).AddSeconds(1));
            var forgotten = store.JoinOrCreateRoom(original.Room.RoomCode, "Taylor", now.AddHours(4).AddDays(60).AddSeconds(2)).Value!;

        Assert.NotEqual(original.Room.OwnerToken, forgotten.Room.OwnerToken);
        Assert.Equal(forgotten.Participant.Id, forgotten.Room.OwnerParticipantId);
    }

    [Fact]
    public void GetRoom_EvictsParticipantWhoStoppedPolling()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", now).Value!;
        var sarah = store.JoinRoom(owner.Room.RoomCode, "Sarah", now.AddSeconds(1)).Value!;

        // Sarah stops polling; owner polls after the timeout window.
        var pollTime = now.AddSeconds(1) + InMemoryScrumPokerStore.ParticipantTimeout + TimeSpan.FromSeconds(1);
        var room = store.GetRoom(owner.Room.RoomCode, owner.Participant.Token, pollTime).Value!;

        Assert.DoesNotContain(room.Participants, p => p.Id == sarah.Participant.Id);
        Assert.Equal(owner.Participant.Id, room.CurrentHostParticipantId);
    }

    [Fact]
    public void GetRoom_EvictsStaleHost_TransfersHostToOwner()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Damian", now).Value!;
        var sarah = store.JoinRoom(owner.Room.RoomCode, "Sarah", now.AddSeconds(1)).Value!;

        // Owner leaves so Sarah becomes temporary host.
        store.Leave(owner.Room.RoomCode, owner.Participant.Token, now.AddSeconds(2));

        // Owner rejoins, then Sarah stops polling.
        store.JoinRoom(owner.Room.RoomCode, "Damian", now.AddSeconds(3), owner.Participant.Token);
        var pollTime = now.AddSeconds(1) + InMemoryScrumPokerStore.ParticipantTimeout + TimeSpan.FromSeconds(1);
        var room = store.GetRoom(owner.Room.RoomCode, owner.Participant.Token, pollTime).Value!;

        Assert.DoesNotContain(room.Participants, p => p.Id == sarah.Participant.Id);
        Assert.Equal(owner.Participant.Id, room.CurrentHostParticipantId);
    }
}
