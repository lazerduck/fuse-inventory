using Fuse.API.Controllers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Fuse.Tests.Controllers;

public class OnboardingControllerTests
{
    private static OnboardingController Controller(InMemoryFuseStore store, Guid? userId) =>
        new(store, Mock.Of<ICurrentUser>(u => u.IsAuthenticated == userId.HasValue && u.UserId == userId));

    private static UserGuideProgress Progress(ActionResult<UserGuideProgress> result) =>
        Assert.IsType<UserGuideProgress>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public async Task ProgressRetainsApplicationAndIsIsolatedPerUser()
    {
        var store = new InMemoryFuseStore();
        var controller = Controller(store, Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var request = new UpdateGuideProgress(["first-app-create"], "first-application", false, null, applicationId);

        await controller.UpdateProgress(request, default);
        var saved = Progress(await controller.GetProgress(default));
        Assert.Equal(applicationId, saved.ApplicationId);
        Assert.Equal(["first-app-create"], saved.CompletedStepIds);
        var other = Progress(await Controller(store, Guid.NewGuid()).GetProgress(default));
        Assert.Null(other.ApplicationId);
        Assert.Empty(other.CompletedStepIds);

        // Older clients can still save progress without an application context.
        var legacy = new UpdateGuideProgress([], "first-application", false, null);
        Assert.Null(Progress(await controller.UpdateProgress(legacy, default)).ApplicationId);
    }

    [Fact]
    public async Task AnonymousUsersCannotReadOrWriteProgress()
    {
        var controller = Controller(new InMemoryFuseStore(), null);
        Assert.IsType<UnauthorizedResult>((await controller.GetProgress(default)).Result);
        Assert.IsType<UnauthorizedResult>((await controller.UpdateProgress(
            new UpdateGuideProgress([], null, false, null), default)).Result);
    }
}
