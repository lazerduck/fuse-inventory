using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using System.Collections.Generic;

namespace Fuse.Core.Interfaces;

public interface ISecretOperationService
{
    Task<Result> CreateSecretAsync(CreateSecret command, string userName, Guid? userId);
    Task<Result> RotateSecretAsync(RotateSecret command, string userName, Guid? userId);
    Task<Result<string>> RevealSecretAsync(RevealSecret command, string userName, Guid? userId);
    Task<Result<IReadOnlyList<SecretMetadata>>> ListSecretsAsync(Guid providerId);
}
