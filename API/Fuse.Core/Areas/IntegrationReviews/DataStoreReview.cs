using Fuse.Core.Models;

namespace Fuse.Core.Areas.IntegrationReviews;

internal static class DataStoreReview
{
    internal static Models.Account? ScopedAccount(Snapshot snapshot, Guid? id, Guid storeId) =>
        snapshot.Accounts.FirstOrDefault(a => a.Id == id && a.TargetKind == TargetKind.DataStore && a.TargetId == storeId);

    internal static string? ValidateScope(Snapshot snapshot, Guid? storeId, string storeKind, ReviewInput input,
        IReadOnlyList<ReviewCheck> checks)
    {
        if (storeId is null || !snapshot.DataStores.Any(d => d.Id == storeId && string.Equals(d.Kind, storeKind, StringComparison.OrdinalIgnoreCase)))
            return "Select a saved integration for this data store kind.";
        var check = checks.FirstOrDefault(c => c.Kind == input.Kind);
        if (check is null) return "Select an available review.";
        if (input.Fields is null || input.Fields.Keys.Any(k => check.Fields.All(f => f.Key != k)))
            return "The review contains an unrecognized field.";
        foreach (var field in check.Fields)
        {
            var value = input.Fields.GetValueOrDefault(field.Key);
            if (field.Required && string.IsNullOrWhiteSpace(value)) return field.Label + " is required.";
            if (value is not null && (value.Length > 1024 || value.Any(char.IsControl) ||
                (field.Choices is not null && !field.Choices.Contains(value)))) return "Invalid " + field.Label + ".";
        }
        if (input.AccountId.HasValue || check.RequiresAccount)
        {
            var account = ScopedAccount(snapshot, input.AccountId, storeId.Value);
            if (account is null || string.IsNullOrWhiteSpace(account.UserName)) return "Select a named account on this integration’s data store.";
        }
        return null;
    }

    internal static ReviewEvidence Compare(bool matches, string summary, params string[] details) =>
        new(matches ? ReviewStatus.Matched : ReviewStatus.Drift, summary, details);
    internal static ReviewEvidence Unknown(string reason, params string[] details) => new(ReviewStatus.Unknown, reason, details);
}
