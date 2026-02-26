namespace Fuse.Core.Commands;

public record UpdatePasswordGeneratorConfig(
    string AllowedCharacters,
    int Length
);
