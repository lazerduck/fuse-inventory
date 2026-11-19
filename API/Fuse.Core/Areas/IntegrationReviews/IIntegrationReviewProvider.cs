using Fuse.Core.Models;

namespace Fuse.Core.Areas.IntegrationReviews;

public sealed record ReviewField(string Key, string Label, bool Required = true, string[]? Choices = null, string? Hint = null);
public sealed record ReviewCheck(string Kind, string Label, IReadOnlyList<ReviewField> Fields, bool RequiresAccount = false);
public sealed record ReviewInput(string Provider, Guid IntegrationId, string Kind, Guid? AccountId, Dictionary<string, string> Fields);
public enum ReviewStatus { Matched, Drift, Unknown }
public sealed record ReviewEvidence(ReviewStatus Status, string Summary, IReadOnlyList<string> Details);
public interface IIntegrationReviewProvider
{
    string Provider { get; }
    IReadOnlyList<ReviewCheck> Checks { get; }
    // Offline validation. Verify the connection and any supplied account refer to the same service scope.
    string? Validate(Snapshot snapshot, ReviewInput input);
    Task<ReviewEvidence> InspectAsync(Snapshot snapshot, ReviewInput input, CancellationToken ct);
}
