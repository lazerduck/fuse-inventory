namespace Fuse.Core.Models;

public record PasswordGeneratorConfig(
    string AllowedCharacters,
    int Length
)
{
    public static readonly PasswordGeneratorConfig Default = new(
        AllowedCharacters: "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+",
        Length: 32
    );
}
