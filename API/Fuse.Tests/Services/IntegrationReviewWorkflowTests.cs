using Fuse.Core.Areas.IntegrationReviews;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fuse.Tests.Services;

public class IntegrationReviewWorkflowTests
{
    private sealed class Observer : IIntegrationReviewProvider
    {
        public string Provider => "redis";
        public IReadOnlyList<ReviewCheck> Checks { get; } = [new("probe", "Probe", [new("sample", "Sample")])];
        public Func<Task<ReviewEvidence>> Next { get; set; } = () => Task.FromResult(new ReviewEvidence(ReviewStatus.Matched, "Confirmed", []));
        public string? Validate(Snapshot s, ReviewInput input) => null;
        public Task<ReviewEvidence> InspectAsync(Snapshot s, ReviewInput input, CancellationToken ct) => Next();
    }

    private readonly Observer observer = new();
    private readonly InMemoryFuseStore store;
    private readonly FakeAuditService audit = new();
    private readonly IntegrationReviewService service;
    private readonly SaveReview command;

    public IntegrationReviewWorkflowTests()
    {
        var node = new RedisIntegration(Guid.NewGuid(), "Cache", Guid.NewGuid(), new Uri("redis://cache:6379"), "reader", null, DateTime.UtcNow, DateTime.UtcNow);
        store = new InMemoryFuseStore(new InMemoryFuseStore().Current! with { RedisIntegrations = [node] });
        service = new IntegrationReviewService(store, [observer], audit, new FakeCurrentUser(), NullLogger<IntegrationReviewService>.Instance);
        command = new("Access", new("redis", node.Id, "probe", null, new() { ["sample"] = "order" }));
    }

    [Fact]
    public async Task UnknownPreservesVerifiedObservationAndConcurrentEditsRequireRevision()
    {
        var review = (await service.SaveAsync(null, command, default)).Value!;
        var first = (await service.RefreshAsync(review.Id, default)).Value!;
        observer.Next = () => Task.FromResult(new ReviewEvidence(ReviewStatus.Unknown, "Restricted", []));
        var later = (await service.RefreshAsync(review.Id, default)).Value!;
        Assert.Equal(first.LastVerified, later.LastVerified);
        Assert.Equal(ReviewStatus.Unknown, later.LastAttempt!.Evidence.Status);
        Assert.Equal(2, later.History.Count);
        var renamed = (await service.SaveAsync(review.Id, command with { Name = "Renamed", ExpectedRevision = review.Revision }, default)).Value!;
        Assert.Equal(later.History, renamed.History);
        Assert.Equal(ErrorType.Conflict, (await service.DeleteAsync(review.Id, review.Revision, default)).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await service.SaveAsync(review.Id, command with { ExpectedRevision = review.Revision }, default)).ErrorType);
        var changed = command with { Input = command.Input with { Fields = new() { ["sample"] = "other" } }, ExpectedRevision = renamed.Revision };
        Assert.Empty((await service.SaveAsync(review.Id, changed, default)).Value!.History);
        Assert.Equal(3, audit.Logs.Count);
    }

    [Fact]
    public async Task ConcurrentRefreshCannotOverwriteANewerObservation()
    {
        var review = (await service.SaveAsync(null, command, default)).Value!;
        var pending = new TaskCompletionSource<ReviewEvidence>(TaskCreationOptions.RunContinuationsAsynchronously);
        observer.Next = () => pending.Task;
        var slow = service.RefreshAsync(review.Id, default);
        observer.Next = () => Task.FromResult(new ReviewEvidence(ReviewStatus.Drift, "Newer", []));
        var newer = (await service.RefreshAsync(review.Id, default)).Value!;
        pending.SetResult(new(ReviewStatus.Matched, "Stale", []));
        Assert.Equal(newer.LastAttempt, (await slow).Value!.LastAttempt);
        Assert.Single(store.Current!.IntegrationReviews.Single().History);
    }

    [Fact]
    public async Task MovingTheRegisteredEndpointInvalidatesRunningAndFutureInspections()
    {
        var review = (await service.SaveAsync(null, command, default)).Value!;
        var pending = new TaskCompletionSource<ReviewEvidence>(TaskCreationOptions.RunContinuationsAsynchronously);
        observer.Next = () => pending.Task;
        var slow = service.RefreshAsync(review.Id, default);
        await store.UpdateAsync(s => s with { RedisIntegrations = [s.RedisIntegrations.Single() with { Endpoint = new Uri("redis://other:6379") }] });
        pending.SetResult(new(ReviewStatus.Matched, "Prior target", []));
        Assert.Equal(ErrorType.Conflict, (await slow).ErrorType);
        var current = (await service.RefreshAsync(review.Id, default)).Value!;
        Assert.Null(current.LastVerified);
        Assert.Equal(ReviewStatus.Unknown, current.LastAttempt!.Evidence.Status);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(15, true)]
    [InlineData(10080, true)]
    [InlineData(10081, false)]
    public async Task ScheduledRefreshIsBoundedAndManualByDefault(int interval, bool allowed)
    {
        var result = await service.SaveAsync(null, command with { RefreshMinutes = interval }, default);
        Assert.Equal(allowed, result.IsSuccess);
    }

    [Fact]
    public async Task UnknownFieldsAndMissingValuesAreRejectedAndHistoryIsBounded()
    {
        foreach (var fields in new Dictionary<string,string>[] { new() { ["extra"] = "value" }, new(), new() { ["sample"] = new string('x',8193) } })
            Assert.False((await service.SaveAsync(null, command with { Input = command.Input with { Fields = fields } }, default)).IsSuccess);
        var review = (await service.SaveAsync(null, command, default)).Value!;
        for (var i = 0; i < 35; i++) await service.RefreshAsync(review.Id, default);
        Assert.Equal(32, store.Current!.IntegrationReviews.Single().History.Count);
    }
}
