using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IPasswordGeneratorService
{
    Task<PasswordGeneratorConfig> GetConfigAsync();
    Task<Result<PasswordGeneratorConfig>> UpdateConfigAsync(UpdatePasswordGeneratorConfig command);
    Task<Result<string>> GeneratePasswordAsync();
}
