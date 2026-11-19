using Fuse.Core.Areas.Audit;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Microsoft.Extensions.Logging;

namespace Fuse.Core.Areas.IntegrationReviews;

public sealed class IntegrationReviewService(IFuseStore store, IEnumerable<IIntegrationReviewProvider> providers,
    IAuditService audit, ICurrentUser user, ILogger<IntegrationReviewService> logger)
{
    public IReadOnlyDictionary<string, IIntegrationReviewProvider> Providers { get; } = providers.ToDictionary(p => p.Provider, StringComparer.Ordinal);
    private const string Missing = "Integration review not found.";

    public async Task<Result<ReviewDefinition>> SaveAsync(Guid? id, SaveReview command, CancellationToken ct)
    {
        var result = Result<ReviewDefinition>.Failure(Missing, ErrorType.NotFound);
        await store.UpdateAsync(s =>
        {
            var existing = s.IntegrationReviews.FirstOrDefault(r => r.Id == id);
            if (id.HasValue && existing is null) return s;
            if (existing is not null && existing.Revision != command.ExpectedRevision)
            {
                result = Result<ReviewDefinition>.Failure("This review has changed. Reload it before editing.", ErrorType.Conflict);
                return s;
            }
            var error = Validate(s, command, existing);
            if (error is not null)
            {
                result = Result<ReviewDefinition>.Failure(error);
                return s;
            }
            var input = command.Input;
            // Only keep observations when the service, account, check and expectation still match.
            var scope = ReviewSources.Scope(s, input);
            var retain = existing is not null && existing.ScopeFingerprint == scope && existing.Input with { Fields = input.Fields } == input
                && existing.Input.Fields.Count == input.Fields.Count && input.Fields.All(pair =>
                    existing.Input.Fields.TryGetValue(pair.Key, out var value) && pair.Value == value);
            var row = new ReviewDefinition(id ?? Guid.NewGuid(), Guid.NewGuid(), command.Name.Trim(), input,
                command.RefreshMinutes, (command.ApplicationInstanceIds ?? []).Distinct().ToArray(),
                retain ? existing!.LastAttempt : null, retain ? existing!.LastVerified : null, retain ? existing!.History : [], scope);
            result = Result<ReviewDefinition>.Success(row);
            return s with { IntegrationReviews = existing is null ? [.. s.IntegrationReviews, row]
                : s.IntegrationReviews.Select(r => r.Id == id ? row : r).ToArray() };
        }, ct);
        if (result.IsSuccess) await Log(id.HasValue ? AuditAction.IntegrationReviewUpdated : AuditAction.IntegrationReviewCreated, result.Value!, ct);
        return result;
    }

    private string? Validate(Snapshot s, SaveReview command, ReviewDefinition? existing)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 150) return "Enter a name of up to 150 characters.";
        if (command.RefreshMinutes != 0 && command.RefreshMinutes is < 15 or > 10080) return "Use manual refresh or a 15–10080 minute interval.";
        var input = command.Input;
        if (input?.Provider is null || !Providers.TryGetValue(input.Provider, out var provider)
            || ReviewSources.Find(s, input) is null) return "Select an available integration.";
        if (existing is not null && (existing.Input.Provider != input.Provider || existing.Input.IntegrationId != input.IntegrationId))
            return "Create a new review to change the integration.";
        var check = provider.Checks.FirstOrDefault(c => c.Kind == input.Kind);
        if (check is null || input.Fields is null) return "Select an available check.";
        if (check.RequiresAccount && input.AccountId is null) return "Select an inventory account.";
        if (input.AccountId.HasValue && !s.Accounts.Any(a => a.Id == input.AccountId)) return "Account not found.";
        if (input.Fields.Count > check.Fields.Count || input.Fields.Any(f => f.Value is null || f.Value.Length > 8192
                || !check.Fields.Any(spec => spec.Key == f.Key))) return "Unexpected or oversized expectation field.";
        foreach (var field in check.Fields)
        {
            input.Fields.TryGetValue(field.Key, out var value);
            if (field.Required && string.IsNullOrWhiteSpace(value)) return "Enter " + field.Label + ".";
            if (!string.IsNullOrEmpty(value) && field.Choices is not null && !field.Choices.Contains(value, StringComparer.Ordinal))
                return "Choose a valid " + field.Label + ".";
        }
        if (command.ApplicationInstanceIds is { Length: > 100 } ||
            (command.ApplicationInstanceIds ?? []).Except(s.Applications.SelectMany(a => a.Instances).Select(i => i.Id)).Any())
            return "Select existing application instances.";
        return provider.Validate(s, input);
    }

    public async Task<Result> DeleteAsync(Guid id, Guid revision, CancellationToken ct)
    {
        ReviewDefinition? removed = null;
        var result = Result.Failure(Missing, ErrorType.NotFound);
        await store.UpdateAsync(s =>
        {
            var current = s.IntegrationReviews.FirstOrDefault(x => x.Id == id);
            if (current is null) return s;
            if (current.Revision != revision)
            {
                result = Result.Failure("This review has changed. Reload it before deleting.", ErrorType.Conflict);
                return s;
            }
            removed = current;
            result = Result.Success();
            return s with { IntegrationReviews = s.IntegrationReviews.Where(r => r.Id != id).ToArray() };
        }, ct);
        if (removed is not null) await Log(AuditAction.IntegrationReviewDeleted, removed, ct);
        return result;
    }

    public async Task<Result<ReviewDefinition>> RefreshAsync(Guid id, CancellationToken ct)
    {
        var snapshot = await store.GetAsync(ct);
        var row = snapshot.IntegrationReviews.FirstOrDefault(review => review.Id == id);
        if (row is null) return Result<ReviewDefinition>.Failure(Missing, ErrorType.NotFound);
        var started = DateTimeOffset.UtcNow;
        var source = ReviewSources.Find(snapshot, row.Input);
        var evidence = new ReviewEvidence(ReviewStatus.Unknown, "The saved connection or account is unavailable.", []);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(50));
        if (source is not null && row.ScopeFingerprint != ReviewSources.Scope(snapshot, row.Input))
            evidence = new(ReviewStatus.Unknown, "The connection or inventory principal changed scope. Review and save the expectation again.", []);
        else if (source is not null && Providers.TryGetValue(row.Input.Provider, out var provider))
        {
            try
            {
                var error = provider.Validate(snapshot, row.Input);
                evidence = error is null ? await provider.InspectAsync(snapshot, row.Input, timeout.Token)
                    : new(ReviewStatus.Unknown, error, []);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            { evidence = new(ReviewStatus.Unknown, "The inspection timed out.", []); }
            catch (Exception error) when (error is not OperationCanceledException && !ct.IsCancellationRequested)
            {
                // Provider exception text can contain private connection information.
                logger.LogWarning("Review {ReviewId} failed with {ErrorType}", id, error.GetType().Name);
                evidence = new(ReviewStatus.Unknown, "The provider could not complete the inspection.", []);
            }
        }
        var attempt = new ReviewAttempt(started, evidence);
        var result = Result<ReviewDefinition>.Failure("The expectation or connection changed during inspection. Refresh again.", ErrorType.Conflict);
        await store.UpdateAsync(s =>
        {
            var current = s.IntegrationReviews.FirstOrDefault(review => review.Id == id);
            if (current?.Revision != row.Revision || ReviewSources.Find(s, row.Input) != source
                || ReviewSources.Scope(s, row.Input) != ReviewSources.Scope(snapshot, row.Input)) return s;
            if (current.LastAttempt?.CheckedAt >= started)
            {
                result = Result<ReviewDefinition>.Success(current);
                return s;
            }
            var updated = current with {
                LastAttempt = attempt,
                LastVerified = evidence.Status == ReviewStatus.Unknown ? current.LastVerified : attempt,
                History = current.History.Prepend(attempt).Take(32).ToArray() };
            result = Result<ReviewDefinition>.Success(updated);
            return s with { IntegrationReviews = s.IntegrationReviews.Select(r => r.Id == id ? updated : r).ToArray() };
        }, ct);
        return result;
    }

    private Task Log(AuditAction action, ReviewDefinition row, CancellationToken ct) =>
        audit.LogAsync(AuditHelper.CreateLog(action, AuditArea.IntegrationReview, user.UserName, user.UserId, row.Id,
            new { row.Input.Provider, row.Input.IntegrationId, row.Input.Kind, row.Revision }), ct);
}
