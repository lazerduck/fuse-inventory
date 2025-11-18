using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface ISecretProviderService
{
    Task<IReadOnlyList<SecretProvider>> GetSecretProvidersAsync();
    Task<SecretProvider?> GetSecretProviderByIdAsync(Guid id);
    Task<Result<SecretProvider>> CreateSecretProviderAsync(CreateSecretProvider command);
    Task<Result<SecretProvider>> UpdateSecretProviderAsync(UpdateSecretProvider command);
    Task<Result> DeleteSecretProviderAsync(DeleteSecretProvider command);
    Task<Result> TestConnectionAsync(TestSecretProviderConnection command);
}
