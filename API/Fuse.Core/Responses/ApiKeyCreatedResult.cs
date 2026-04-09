namespace Fuse.Core.Responses;

public record ApiKeyCreatedResult(
    ApiKeyInfo Info,
    string PlainTextKey
);
