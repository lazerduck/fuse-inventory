using Fuse.API.Controllers;
using Fuse.Core.Areas.ScrumPoker;
using Fuse.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Fuse.Tests.Controllers;

public sealed class ScrumPokerControllerTests
{
    [Fact]
    public async Task RoomOperations_ReturnNotFoundWhenFeatureIsDisabled()
    {
        var inventoryStore = new InMemoryFuseStore();
        var controller = CreateController(inventoryStore);

        var result = await controller.CreateRoom(new ScrumPokerJoinRequest("Alice"));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task StateRedactsOtherParticipantsCardsUntilReveal()
    {
        var inventoryStore = new InMemoryFuseStore();
        await inventoryStore.UpdateAsync(snapshot => snapshot with
        {
            AppSettings = snapshot.AppSettings with { ScrumPokerEnabled = true }
        });
        var controller = CreateController(inventoryStore);

        var createResult = await controller.CreateRoom(new ScrumPokerJoinRequest("Alice"));
        var session = GetSession(createResult);
        var joinResult = await controller.JoinRoom(session.RoomCode, new ScrumPokerJoinRequest("Bob"));
        var guest = GetSession(joinResult);

        await controller.SelectCard(session.RoomCode, new ScrumPokerCardRequest(session.ParticipantToken, ScrumPokerCard.Five));
        await controller.SelectCard(session.RoomCode, new ScrumPokerCardRequest(guest.ParticipantToken, ScrumPokerCard.Eight));

        var beforeReveal = GetRoom(await controller.GetState(session.RoomCode, session.ParticipantToken));
        var aliceBeforeReveal = beforeReveal.Participants.Single(p => p.DisplayName == "Alice");
        var bobBeforeReveal = beforeReveal.Participants.Single(p => p.DisplayName == "Bob");
        Assert.Equal(ScrumPokerCard.Five, aliceBeforeReveal.Card);
        Assert.True(aliceBeforeReveal.HasVoted);
        Assert.Null(bobBeforeReveal.Card);
        Assert.True(bobBeforeReveal.HasVoted);

        var guestReveal = GetRoom(await controller.Reveal(session.RoomCode, new ScrumPokerParticipantRequest(guest.ParticipantToken)));
        Assert.Equal(ScrumPokerPhase.Revealed, guestReveal.Phase);
        var afterReveal = GetRoom(await controller.GetState(session.RoomCode, session.ParticipantToken));

        Assert.Equal(ScrumPokerCard.Five, afterReveal.Participants.Single(p => p.DisplayName == "Alice").Card);
        Assert.Equal(ScrumPokerCard.Eight, afterReveal.Participants.Single(p => p.DisplayName == "Bob").Card);
        Assert.Equal(6.5, afterReveal.Average);

        var hidden = GetRoom(await controller.Hide(session.RoomCode, new ScrumPokerParticipantRequest(session.ParticipantToken)));
        Assert.Equal(ScrumPokerPhase.Voting, hidden.Phase);
        Assert.Null(hidden.Participants.Single(p => p.DisplayName == "Bob").Card);
        Assert.True(hidden.Participants.Single(p => p.DisplayName == "Bob").HasVoted);
    }

    [Fact]
    public async Task AutoRevealSetting_IsSharedAcrossParticipants()
    {
        var inventoryStore = new InMemoryFuseStore();
        await inventoryStore.UpdateAsync(snapshot => snapshot with
        {
            AppSettings = snapshot.AppSettings with { ScrumPokerEnabled = true }
        });
        var controller = CreateController(inventoryStore);

        var owner = GetSession(await controller.CreateRoom(new ScrumPokerJoinRequest("Alice")));
        var guest = GetSession(await controller.JoinRoom(owner.RoomCode, new ScrumPokerJoinRequest("Bob")));

        var updated = GetRoom(await controller.SetAutoReveal(owner.RoomCode, new ScrumPokerAutoRevealRequest(guest.ParticipantToken, true)));
        var ownerView = GetRoom(await controller.GetState(owner.RoomCode, owner.ParticipantToken));

        Assert.True(updated.AutoReveal);
        Assert.True(ownerView.AutoReveal);
    }

    private static ScrumPokerController CreateController(InMemoryFuseStore inventoryStore) =>
        new(new InMemoryScrumPokerStore(), inventoryStore);

    private static ScrumPokerSessionResponse GetSession(ActionResult<ScrumPokerSessionResponse> result) =>
        Assert.IsType<ScrumPokerSessionResponse>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    private static ScrumPokerRoomResponse GetRoom(ActionResult<ScrumPokerRoomResponse> result) =>
        Assert.IsType<ScrumPokerRoomResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
