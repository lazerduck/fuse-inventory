using Fuse.Core.Areas.ScrumPoker;
using Fuse.Core.Helpers;
using Xunit;

namespace Fuse.Tests.Services;

public sealed class ScrumPokerStoreTests
{
    private static readonly DateTime Start = new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateRoom_ReturnsRoomAndPrivateParticipantToken()
    {
        var store = new InMemoryScrumPokerStore();

        var result = store.CreateRoom(" Alice ", Start);

        Assert.True(result.IsSuccess);
        Assert.Equal("Alice", result.Value!.Participant.DisplayName);
        Assert.Equal(result.Value.Participant.Id, Assert.Single(result.Value.Room.Participants).Id);
        Assert.NotEmpty(result.Value.Participant.Token);
        Assert.NotEmpty(result.Value.OwnerToken);
        Assert.Equal(ScrumPokerPhase.Voting, result.Value.Room.Phase);
        Assert.Equal(1, result.Value.Room.Round);
        Assert.False(result.Value.Room.AutoReveal);
    }

    [Fact]
    public void JoinRoom_AllowsDuplicateDisplayNamesBecauseNamesAreNotIdentity()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;

        var result = store.JoinRoom(owner.Room.RoomCode, " alice ", Start.AddSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.NotEqual(owner.Participant.Id, result.Value!.Participant.Id);
    }

    [Fact]
    public void SelectCard_OnlyChangesTheParticipantWhoOwnsTheToken()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        var guest = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(1)).Value!;

        var result = store.SelectCard(owner.Room.RoomCode, guest.Participant.Token, ScrumPokerCard.Eight, Start.AddSeconds(2));

        Assert.True(result.IsSuccess);
        var alice = result.Value!.Participants.Single(p => p.DisplayName == "Alice");
        var bob = result.Value.Participants.Single(p => p.DisplayName == "Bob");
        Assert.Null(alice.SelectedCard);
        Assert.Equal(ScrumPokerCard.Eight, bob.SelectedCard);
    }

    [Fact]
    public void SelectCard_CanClearAnExistingVote()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, ScrumPokerCard.Eight, Start.AddSeconds(1));

        var result = store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, null, Start.AddSeconds(2));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Participants.Single().SelectedCard);
    }

    [Fact]
    public void GetRoom_RetainsCardsForThePublicProjectionToRedact()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, ScrumPokerCard.Five, Start.AddSeconds(1));

        var result = store.GetRoom(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(ScrumPokerCard.Five, result.Value!.Participants.Single().SelectedCard);
        // The store retains the value; the API projection will redact it before reveal.
        Assert.Equal(ScrumPokerPhase.Voting, result.Value.Phase);
    }

    [Fact]
    public void Reveal_IsRestrictedToTheCurrentHostAndIsIdempotent()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        var guest = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(1)).Value!;

        var unauthorized = store.Reveal(owner.Room.RoomCode, guest.Participant.Token, Start.AddSeconds(2));
        var first = store.Reveal(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(3));
        var second = store.Reveal(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(4));

        Assert.False(unauthorized.IsSuccess);
        Assert.Equal(ScrumPokerPhase.Revealed, first.Value!.Phase);
        Assert.Equal(first.Value.Revision, second.Value!.Revision);
    }

    [Fact]
    public void SetAutoReveal_IsSharedForAllParticipantsAndDefaultsOff()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        var guest = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(1)).Value!;

        Assert.False(owner.Room.AutoReveal);

        var unauthorized = store.SetAutoReveal(owner.Room.RoomCode, guest.Participant.Token, true, Start.AddSeconds(2));
        var updated = store.SetAutoReveal(owner.Room.RoomCode, owner.Participant.Token, true, Start.AddSeconds(3));
        var ownerView = store.GetRoom(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(4));

        Assert.False(unauthorized.IsSuccess);
        Assert.True(updated.IsSuccess);
        Assert.True(updated.Value!.AutoReveal);
        Assert.True(ownerView.Value!.AutoReveal);
    }

    [Fact]
    public void SelectCard_AutoRevealsWhenEnabledAndEveryoneVoted()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        var guest = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(1)).Value!;

        store.SetAutoReveal(owner.Room.RoomCode, owner.Participant.Token, true, Start.AddSeconds(2));
        store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, ScrumPokerCard.Five, Start.AddSeconds(3));

        var afterSecondVote = store.SelectCard(owner.Room.RoomCode, guest.Participant.Token, ScrumPokerCard.Eight, Start.AddSeconds(4));

        Assert.True(afterSecondVote.IsSuccess);
        Assert.Equal(ScrumPokerPhase.Revealed, afterSecondVote.Value!.Phase);
    }

    [Fact]
    public void Hide_ReturnsTheRoomToVotingWithoutClearingSelections()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, ScrumPokerCard.Five, Start.AddSeconds(1));
        store.Reveal(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(2));

        var result = store.Hide(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(3));

        Assert.True(result.IsSuccess);
        Assert.Equal(ScrumPokerPhase.Voting, result.Value!.Phase);
        Assert.Equal(ScrumPokerCard.Five, result.Value.Participants.Single().SelectedCard);
    }

    [Fact]
    public void Reset_ClearsCardsAndStartsANewRoundForEveryone()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        var guest = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(1)).Value!;
        store.SelectCard(owner.Room.RoomCode, owner.Participant.Token, ScrumPokerCard.Five, Start.AddSeconds(2));
        store.SelectCard(owner.Room.RoomCode, guest.Participant.Token, ScrumPokerCard.Eight, Start.AddSeconds(3));
        store.Reveal(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(4));

        var result = store.Reset(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Round);
        Assert.Equal(ScrumPokerPhase.Voting, result.Value.Phase);
        Assert.All(result.Value.Participants, participant => Assert.Null(participant.SelectedCard));
    }

    [Fact]
    public void RoomsExpireAtTheConfiguredInactivityBoundary()
    {
        var store = new InMemoryScrumPokerStore(TimeSpan.FromMinutes(10));
        var owner = store.CreateRoom("Alice", Start).Value!;

        var joinBeforeExpiry = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddMinutes(10).AddTicks(-1));
        var readBeforeExpiry = store.GetRoom(owner.Room.RoomCode, joinBeforeExpiry.Value!.Participant.Token, Start.AddMinutes(20).AddTicks(-2));
        var readAtExpiry = store.GetRoom(owner.Room.RoomCode, joinBeforeExpiry.Value.Participant.Token, Start.AddMinutes(30).AddTicks(-2));

        Assert.True(joinBeforeExpiry.IsSuccess);
        Assert.True(readBeforeExpiry.IsSuccess);
        Assert.False(readAtExpiry.IsSuccess);
        Assert.Equal(ErrorType.NotFound, readAtExpiry.ErrorType);
    }

    [Fact]
    public void Leave_RemovesParticipantAndAllowsTheSameNameToRejoin()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        var guest = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(1)).Value!;

        var left = store.Leave(owner.Room.RoomCode, guest.Participant.Token, Start.AddSeconds(2));
        var rejoined = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(3));

        Assert.True(left.IsSuccess);
        Assert.DoesNotContain(left.Value!.Participants, participant => participant.DisplayName == "Bob");
        Assert.True(rejoined.IsSuccess);
        Assert.NotEqual(guest.Participant.Token, rejoined.Value!.Participant.Token);
    }

    [Fact]
    public void RemoveParticipant_DoesNotAllowTheRemovedTokenToRejoin()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        var guest = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(1)).Value!;

        var removed = store.RemoveParticipant(owner.Room.RoomCode, owner.Room.OwnerToken, guest.Participant.Id, Start.AddSeconds(2));
        var rejoin = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(3), guest.Participant.Token);

        Assert.True(removed.IsSuccess);
        Assert.False(rejoin.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, rejoin.ErrorType);

        var manualRejoin = store.JoinRoom(
            owner.Room.RoomCode,
            "Bob",
            Start.AddSeconds(4),
            guest.Participant.Token,
            allowRemovedParticipantAsNew: true);

        Assert.True(manualRejoin.IsSuccess);
        Assert.NotEqual(guest.Participant.Id, manualRejoin.Value!.Participant.Id);
    }

    [Fact]
    public void Leave_PreservesOwnershipAndTransfersCurrentHost()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        var guest = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(1)).Value!;

        var left = store.Leave(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(2));
        var reveal = store.Reveal(owner.Room.RoomCode, guest.Participant.Token, Start.AddSeconds(3));

        Assert.True(left.IsSuccess);
        Assert.True(reveal.IsSuccess);
        Assert.Equal(owner.Participant.Id, left.Value!.OwnerParticipantId);
        Assert.Equal(ScrumPokerPhase.Revealed, reveal.Value!.Phase);
    }

    [Fact]
    public void EmptyRoom_CanBeRejoinedAndNewParticipantBecomesTemporaryHost()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;

        var left = store.Leave(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(1));
        var rejoined = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(20));
        var reveal = store.Reveal(owner.Room.RoomCode, rejoined.Value!.Participant.Token, Start.AddSeconds(21));

        Assert.True(left.IsSuccess);
        Assert.True(rejoined.IsSuccess);
        Assert.True(reveal.IsSuccess);
        Assert.Equal(owner.Participant.Id, rejoined.Value!.Room.OwnerParticipantId);
        Assert.Equal(rejoined.Value.Participant.Id, rejoined.Value.Room.CurrentHostParticipantId);
    }

    [Fact]
    public void EmptyRoom_IsRetainedIndefinitely()
    {
        var store = new InMemoryScrumPokerStore();
        var owner = store.CreateRoom("Alice", Start).Value!;
        store.Leave(owner.Room.RoomCode, owner.Participant.Token, Start.AddSeconds(1));

        var result = store.JoinRoom(owner.Room.RoomCode, "Bob", Start.AddSeconds(32));

        Assert.True(result.IsSuccess);
        Assert.Equal(owner.Participant.Id, result.Value!.Room.OwnerParticipantId);
        Assert.Equal(result.Value.Participant.Id, result.Value.Room.CurrentHostParticipantId);
    }

    [Fact]
    public void JoinOrCreateRoom_CreatesTheRoomUsingTheUrlCodeWhenMissing()
    {
        var store = new InMemoryScrumPokerStore();

        var result = store.JoinOrCreateRoom("team-planning", "Alice", Start);

        Assert.True(result.IsSuccess);
        Assert.Equal("TEAM-PLANNING", result.Value!.Room.RoomCode);
    }
}
