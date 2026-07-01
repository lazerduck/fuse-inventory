using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.SecretProvider;

public interface ISecretProviderService
{
    Task<IReadOnlyList<Models.SecretProvider>> GetSecretProvidersAsync();
    Task<Models.SecretProvider?> GetSecretProviderByIdAsync(Guid id);
    Task<Result<Models.SecretProvider>> CreateSecretProviderAsync(CreateSecretProvider command);
    Task<Result<Models.SecretProvider>> UpdateSecretProviderAsync(UpdateSecretProvider command);
    Task<Result> DeleteSecretProviderAsync(DeleteSecretProvider command);
    Task<Result> TestConnectionAsync(TestSecretProviderConnection command);
}
