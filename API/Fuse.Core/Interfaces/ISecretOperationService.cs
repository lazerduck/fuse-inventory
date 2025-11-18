using Fuse.Core.Commands;
using Fuse.Core.Helpers;

namespace Fuse.Core.Interfaces;

public interface ISecretOperationService
{
    Task<Result> CreateSecretAsync(CreateSecret command, string userName, Guid? userId);
    Task<Result> RotateSecretAsync(RotateSecret command, string userName, Guid? userId);
    Task<Result<string>> RevealSecretAsync(RevealSecret command, string userName, Guid? userId);
}
