namespace Fuse.Core.Helpers;

/// <summary>MongoDB usernames are only unique within their authentication database.</summary>
public static class MongoDbIdentity
{
    public const string ParameterName = "authSource";
    public static string? AuthSource(IReadOnlyDictionary<string, string>? parameters) =>
        parameters is not null && parameters.TryGetValue(ParameterName, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
