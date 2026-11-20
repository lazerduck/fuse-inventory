using System.Text.Json;
using System.Text.RegularExpressions;
using Fuse.Core.Areas.AzureMetadata;
using Fuse.Core.Areas.IntegrationReviews;

namespace Fuse.Core.Areas.ServiceBusIntegration;

internal static partial class ServiceBusFanOut
{
    private record SqlRule(string? Expression);
    internal record Rule(string Subscription, string Name, bool Expected);
    [GeneratedRegex("""\A(?:user\.)?([a-z_][a-z0-9_]*)\s*=\s*'((?:[^']|'')*)'\z""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex Equality();

    internal static Rule[]? Rules(string value)
    {
        try
        {
            using var doc = JsonDocument.Parse(value);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var rules = new List<Rule>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in doc.RootElement.EnumerateObject())
            {
                var parts = row.Name.Split('/');
                if (parts.Length != 2 || !ServiceBusInspector.ValidQueueSegment(parts[0]) ||
                    !(ServiceBusInspector.ValidQueueSegment(parts[1]) || parts[1] == "$Default") ||
                    row.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || !names.Add(row.Name) || rules.Count >= 12) return null;
                rules.Add(new(parts[0], parts[1], row.Value.GetBoolean()));
            }
            return rules.Count == 0 ? null : rules.ToArray();
        }
        catch (JsonException) { return null; }
    }

    internal static async Task<ReviewEvidence> Check(ArmMetadataSession session, string topic, ReviewInput input)
    {
        var evidence = new AzureReview();
        foreach (var rule in Rules(input.Fields["rules"])!)
        {
            var label = rule.Subscription + "/" + rule.Name;
            var result = await session.Resource(topic + "/subscriptions/" + rule.Subscription + "/rules/" + Uri.EscapeDataString(rule.Name), row =>
            {
                var props = row.GetProperty("properties");
                return new SqlRule(props.TryGetProperty("sqlFilter", out var filter) && filter.ValueKind == JsonValueKind.Object
                    ? ArmMetadataSession.Text(filter, "sqlExpression") : null);
            });
            var actual = Evaluate(result.Value?.Expression, input.Fields["property"], input.Fields["value"]);
            if (actual is null) evidence.Unknown(label + ": rule unavailable or expression outside supported SQL examples (" + result.Status + ").");
            else evidence.Compare(label, rule.Expected ? "matches" : "does not match", actual.Value ? "matches" : "does not match");
        }
        return evidence.Result("Named subscription rules for " + input.Fields["topic"]);
    }

    private static bool? Evaluate(string? expression, string property, string value)
    {
        if (expression is null || expression.Length > 2048) return null;
        var trimmed = expression.Trim();
        if (Regex.IsMatch(trimmed, @"\A1\s*=\s*[01]\z", RegexOptions.CultureInvariant)) return trimmed[^1] == '1';
        var match = Equality().Match(trimmed);
        if (!match.Success || !string.Equals(property, match.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) return null;
        return value.Equals(match.Groups[2].Value.Replace("''", "'"), StringComparison.Ordinal);
    }
}
