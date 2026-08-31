using Fuse.Core.Areas.ScrumPoker;
using Xunit;

namespace Fuse.Tests.Areas.ScrumPoker;

public class InMemoryScrumPokerStoreTests
{
    [Fact]
    public void AvatarImageIdentifier_IsAcceptedAndNormalized()
    {
        var session = new InMemoryScrumPokerStore().CreateRoom("Damian", DateTime.UtcNow, "AVATAR-IMAGE-4").Value!;
        Assert.Equal("avatar-image-4", session.Participant.AvatarColor);
    }

    [Fact]
    public void JoinOrCreateRoom_RecreatesExpiredRoomWithSameCode()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore(TimeSpan.FromHours(4));
        var original = store.JoinOrCreateRoom("TEAM42", "Damian", now).Value!;
        var recreated = store.JoinOrCreateRoom("TEAM42", "Sarah", now.AddHours(4)).Value!;

        Assert.Equal(original.Room.RoomCode, recreated.Room.RoomCode);
        Assert.Equal("Sarah", recreated.Participant.DisplayName);
        Assert.Single(recreated.Room.Participants);
    }

    [Fact]
    public void ActiveParticipantActivityExtendsRoomLifetime()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore(TimeSpan.FromHours(4));
        var participant = store.CreateRoom("Damian", now).Value!;

        Assert.True(store.GetRoom(participant.Room.RoomCode, participant.Participant.Token, now.AddHours(3)).IsSuccess);
        Assert.True(store.RoomExists(participant.Room.RoomCode, now.AddHours(6)));
        Assert.False(store.RoomExists(participant.Room.RoomCode, now.AddHours(7).AddSeconds(1)));
    }

    [Fact]
    public void AnyActiveParticipant_CanOperateRoomAndChangeSettings()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var first = store.CreateRoom("Damian", now).Value!;
        var second = store.JoinRoom(first.Room.RoomCode, "Sarah", now.AddSeconds(1)).Value!;

        Assert.True(store.SetAutoReveal(first.Room.RoomCode, second.Participant.Token, true, now.AddSeconds(2)).IsSuccess);
        Assert.True(store.SetLockVotesAfterReveal(first.Room.RoomCode, second.Participant.Token, true, now.AddSeconds(3)).IsSuccess);
        Assert.True(store.Reveal(first.Room.RoomCode, second.Participant.Token, now.AddSeconds(4)).IsSuccess);
        Assert.True(store.Hide(first.Room.RoomCode, second.Participant.Token, now.AddSeconds(5)).IsSuccess);
        Assert.True(store.Reset(first.Room.RoomCode, second.Participant.Token, now.AddSeconds(6)).IsSuccess);
    }

    [Fact]
    public void GetRoom_EvictsParticipantWhoStoppedPolling()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var active = store.CreateRoom("Damian", now).Value!;
        var stale = store.JoinRoom(active.Room.RoomCode, "Sarah", now.AddSeconds(1)).Value!;

        var room = store.GetRoom(active.Room.RoomCode, active.Participant.Token,
            now.AddSeconds(1) + InMemoryScrumPokerStore.ParticipantTimeout).Value!;

        Assert.DoesNotContain(room.Participants, participant => participant.Id == stale.Participant.Id);
    }

    [Fact]
    public void StaleParticipantEviction_CanTriggerAutoReveal()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var active = store.CreateRoom("Damian", now).Value!;
        store.JoinRoom(active.Room.RoomCode, "Sarah", now.AddSeconds(1));
        store.SetAutoReveal(active.Room.RoomCode, active.Participant.Token, true, now.AddSeconds(2));
        store.SelectCard(active.Room.RoomCode, active.Participant.Token, ScrumPokerCard.Five, now.AddSeconds(3));

        var room = store.GetRoom(active.Room.RoomCode, active.Participant.Token,
            now.AddSeconds(1) + InMemoryScrumPokerStore.ParticipantTimeout).Value!;

        Assert.Equal(ScrumPokerPhase.Revealed, room.Phase);
    }
}
