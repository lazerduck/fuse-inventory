using System.Globalization;
using System.Text.Json;
using Fuse.Core.Areas.IntegrationReviews;

namespace Fuse.Core.Areas.AzureMetadata;

internal sealed class AzureReview
{
    private readonly List<string> details = [];
    private bool drift;
    private bool incomplete;

    public void Compare(string label, string expected, string? actual)
    {
        if (actual is null) { Unknown(label + " was not visible."); return; }
        bool matches = string.Equals(expected, actual, StringComparison.Ordinal);
        if (!matches) drift = true;
        details.Add(matches ? $"{label}: {Display(actual)}" : $"{label}: expected {Display(expected)}, observed {Display(actual)}");
    }

    private static string Display(string value) => value.Length <= 1000 ? value : value[..997] + "...";

    public void Unknown(string reason)
    {
        incomplete = true;
        details.Add(reason);
    }

    public ReviewEvidence Result(string target) =>
        new(drift ? ReviewStatus.Drift : incomplete || details.Count == 0 ? ReviewStatus.Unknown : ReviewStatus.Matched,
            target, details);

    public static string? Field(ReviewInput input, string key) =>
        input.Fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public static bool Integer(string? value, int min) => value is null ||
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) && number >= min;

    // Index policy path sets and the outer set of composite indexes are unordered.
    // Within a composite index (or a hierarchical partition key), position is significant.
    public static string Canonical(JsonElement element, string? key = null) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value, p.Name))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", Elements(element, key)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(element.GetString()),
        _ => element.GetRawText()
    };

    private static IEnumerable<string> Elements(JsonElement array, string? key)
    {
        var values = array.EnumerateArray().Select(v => Canonical(v));
        return key is "includedPaths" or "excludedPaths" or "compositeIndexes" or "spatialIndexes" or "types"
            ? values.Order(StringComparer.Ordinal) : values;
    }
}
