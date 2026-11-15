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

    [Theory]
    [InlineData(60)]
    [InlineData(899)]
    public void GetRoom_KeepsBackgroundParticipantAndVoteBeforeFifteenMinutes(int inactiveSeconds)
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var active = store.CreateRoom("Damian", now).Value!;
        var background = store.JoinRoom(active.Room.RoomCode, "Sarah", now).Value!;
        store.SelectCard(active.Room.RoomCode, background.Participant.Token, ScrumPokerCard.Eight, now);

        var room = store.GetRoom(active.Room.RoomCode, active.Participant.Token,
            now.AddSeconds(inactiveSeconds)).Value!;

        var participant = Assert.Single(room.Participants, p => p.Id == background.Participant.Id);
        Assert.Equal(ScrumPokerCard.Eight, participant.SelectedCard);
    }

    [Fact]
    public void GetRoom_EvictsParticipantWhoStoppedPolling()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var active = store.CreateRoom("Damian", now).Value!;
        var stale = store.JoinRoom(active.Room.RoomCode, "Sarah", now.AddSeconds(1)).Value!;

        var room = store.GetRoom(active.Room.RoomCode, active.Participant.Token,
            now.AddSeconds(1).AddMinutes(15)).Value!;

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

    [Fact(Skip = "SP-002: vote preservation after the 15-minute timeout is deferred; see Tests/UI_TEST_FINDINGS.md.")]
    public void ReturnAfterTimeout_RestoresIdentityAvatarAndLatestVoteInSameRound()
    {
        var now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        var store = new InMemoryScrumPokerStore();
        var host = store.CreateRoom("Host", now).Value!;
        var guest = store.JoinRoom(host.Room.RoomCode, "Guest", now, avatarColor: "avatar-image-4").Value!;
        store.SelectCard(host.Room.RoomCode, guest.Participant.Token, ScrumPokerCard.Five, now.AddSeconds(1));
        store.SelectCard(host.Room.RoomCode, guest.Participant.Token, ScrumPokerCard.Eight, now.AddSeconds(2));

        var afterTimeout = now.AddMinutes(16);
        var room = store.GetRoom(host.Room.RoomCode, host.Participant.Token, afterTimeout).Value!;
        Assert.Single(room.Participants);
        var rejoined = store.JoinRoom(host.Room.RoomCode, "Guest", afterTimeout, guest.Participant.Token).Value!;

        Assert.Equal(guest.Participant.Id, rejoined.Participant.Id);
        Assert.Equal(guest.Participant.Token, rejoined.Participant.Token);
        Assert.Equal("avatar-image-4", rejoined.Participant.AvatarColor);
        Assert.Equal(ScrumPokerCard.Eight, rejoined.Participant.SelectedCard);
    }

    [Fact]
    public void ReturnAfterTimeoutAndReset_DoesNotRestoreAPreviousRoundsVote()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var host = store.CreateRoom("Host", now).Value!;
        var guest = store.JoinRoom(host.Room.RoomCode, "Guest", now).Value!;
        store.SelectCard(host.Room.RoomCode, guest.Participant.Token, ScrumPokerCard.Eight, now);
        // Leave saves a participant with a vote in the recovery registry.
        store.Leave(host.Room.RoomCode, guest.Participant.Token, now);
        store.JoinRoom(host.Room.RoomCode, "Guest", now, guest.Participant.Token);
        store.GetRoom(host.Room.RoomCode, host.Participant.Token, now.AddMinutes(16));
        store.Reset(host.Room.RoomCode, host.Participant.Token, now.AddMinutes(16));

        var rejoined = store.JoinRoom(host.Room.RoomCode, "Guest", now.AddMinutes(17), guest.Participant.Token).Value!;

        Assert.Equal(guest.Participant.Id, rejoined.Participant.Id);
        Assert.Equal(2, rejoined.Room.Round);
        Assert.Null(rejoined.Participant.SelectedCard);
    }

    [Fact]
    public void RejoiningAnActiveParticipantInAFullRoom_DoesNotCreateADuplicate()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var host = store.CreateRoom("Host", now).Value!;
        for (var i = 1; i < InMemoryScrumPokerStore.MaxParticipantsPerRoom; i++)
            Assert.True(store.JoinRoom(host.Room.RoomCode, $"Guest {i}", now).IsSuccess);

        var rejoined = store.JoinRoom(host.Room.RoomCode, "Host", now.AddSeconds(1), host.Participant.Token);

        Assert.True(rejoined.IsSuccess);
        Assert.Equal(host.Participant.Id, rejoined.Value!.Participant.Id);
        Assert.Equal(InMemoryScrumPokerStore.MaxParticipantsPerRoom, rejoined.Value.Room.Participants.Count);
        Assert.False(store.JoinRoom(host.Room.RoomCode, "Overflow", now.AddSeconds(1)).IsSuccess);
    }

    [Fact]
    public async Task ConcurrentJoinsAndVotes_KeepDistinctParticipantsAndAllVotes()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryScrumPokerStore();
        var host = store.CreateRoom("Host", now).Value!;
        var guests = await Task.WhenAll(Enumerable.Range(1, 19).Select(i => Task.Run(() =>
            store.JoinRoom(host.Room.RoomCode, $"Guest {i}", now))));
        Assert.All(guests, result => Assert.True(result.IsSuccess));
        var participants = guests.Select(result => result.Value!.Participant).Append(host.Participant).ToArray();
        Assert.Equal(20, participants.Select(participant => participant.Id).Distinct().Count());

        var votes = await Task.WhenAll(participants.Select(participant => Task.Run(() =>
            store.SelectCard(host.Room.RoomCode, participant.Token, ScrumPokerCard.Five, now.AddSeconds(1)))));
        Assert.All(votes, result => Assert.True(result.IsSuccess));
        var room = store.GetRoom(host.Room.RoomCode, host.Participant.Token, now.AddSeconds(2)).Value!;
        Assert.Equal(20, room.Participants.Count);
        Assert.All(room.Participants, participant => Assert.Equal(ScrumPokerCard.Five, participant.SelectedCard));
    }
}
