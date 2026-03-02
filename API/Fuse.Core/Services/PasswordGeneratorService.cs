using System.Security.Cryptography;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class PasswordGeneratorService : IPasswordGeneratorService
{
    private readonly IFuseStore _fuseStore;

    public PasswordGeneratorService(IFuseStore fuseStore)
    {
        _fuseStore = fuseStore;
    }

    public async Task<PasswordGeneratorConfig> GetConfigAsync()
    {
        var store = await _fuseStore.GetAsync();
        return store.PasswordGeneratorConfig ?? PasswordGeneratorConfig.Default;
    }

    public async Task<Result<PasswordGeneratorConfig>> UpdateConfigAsync(UpdatePasswordGeneratorConfig command)
    {
        if (string.IsNullOrEmpty(command.AllowedCharacters))
            return Result<PasswordGeneratorConfig>.Failure("Allowed characters must not be empty.", ErrorType.Validation);

        if (command.AllowedCharacters.Length < 2)
            return Result<PasswordGeneratorConfig>.Failure("Allowed characters must contain at least 2 distinct characters.", ErrorType.Validation);

        if (command.Length < 8)
            return Result<PasswordGeneratorConfig>.Failure("Password length must be at least 8.", ErrorType.Validation);

        if (command.Length > 256)
            return Result<PasswordGeneratorConfig>.Failure("Password length must not exceed 256.", ErrorType.Validation);

        var config = new PasswordGeneratorConfig(command.AllowedCharacters, command.Length);
        await _fuseStore.UpdateAsync(s => s with { PasswordGeneratorConfig = config });
        return Result<PasswordGeneratorConfig>.Success(config);
    }

    public async Task<Result<string>> GeneratePasswordAsync()
    {
        var config = await GetConfigAsync();

        if (string.IsNullOrEmpty(config.AllowedCharacters))
            return Result<string>.Failure("No allowed characters configured.", ErrorType.Validation);

        var chars = config.AllowedCharacters.Distinct().ToArray();
        if (chars.Length == 0)
            return Result<string>.Failure("No allowed characters configured.", ErrorType.Validation);

        var password = GenerateSecure(chars, config.Length);
        return Result<string>.Success(password);
    }

    private static string GenerateSecure(char[] allowedChars, int length)
    {
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = allowedChars[RandomNumberGenerator.GetInt32(allowedChars.Length)];
        }
        return new string(result);
    }
}
