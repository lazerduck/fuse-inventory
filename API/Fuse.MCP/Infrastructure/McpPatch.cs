using ModelContextProtocol;

namespace Fuse.MCP;

internal static class McpPatch
{
    public static void Current(DateTime actual, DateTime expected)
    {
        if (actual.ToUniversalTime() != expected.ToUniversalTime())
            throw new McpException($"The record changed after it was read. Read it again and retry with updatedAt '{actual:O}'.");
    }

    public static void ValidateClears(IReadOnlyList<string>? fields, params string[] allowed)
    {
        if (fields is null) return;
        var set = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = fields.FirstOrDefault(field => !set.Contains(field));
        if (invalid is not null) throw new McpException($"Field '{invalid}' cannot be cleared by this tool.");
    }

    public static bool Clears(IReadOnlyList<string>? fields, string field) =>
        fields?.Contains(field, StringComparer.OrdinalIgnoreCase) == true;

    public static string? Text(string? supplied, string? current, IReadOnlyList<string>? clears, string field) =>
        Clears(clears, field) ? supplied is null ? null : throw Conflict(field) : supplied ?? current;

    public static T? Value<T>(T? supplied, T? current, IReadOnlyList<string>? clears, string field) where T : struct =>
        Clears(clears, field) ? supplied is null ? null : throw Conflict(field) : supplied ?? current;

    public static Uri? Uri(string? supplied, Uri? current, IReadOnlyList<string>? clears, string field)
    {
        if (Clears(clears, field)) return supplied is null ? null : throw Conflict(field);
        if (supplied is null) return current;
        return System.Uri.TryCreate(supplied, UriKind.Absolute, out var uri)
            ? uri : throw new McpException($"'{field}' must be an absolute URI.");
    }

    public static HashSet<Guid> Tags(IReadOnlyList<Guid>? supplied, HashSet<Guid> current, IReadOnlyList<string>? clears) =>
        Clears(clears, "tagIds") ? supplied is null ? [] : throw Conflict("tagIds") : supplied?.ToHashSet() ?? current;

    private static McpException Conflict(string field) => new($"'{field}' cannot be supplied and cleared in the same update.");
}
