using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.KeycloakIntegration;

public interface IKeycloakIntegrationService
{
    Task<IReadOnlyList<KeycloakIntegrationResponse>> GetAllAsync(CancellationToken ct = default);
    Task<KeycloakIntegrationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<KeycloakIntegrationResponse>> CreateAsync(CreateKeycloakIntegration command, CancellationToken ct = default);
    Task<Result<KeycloakIntegrationResponse>> UpdateAsync(UpdateKeycloakIntegration command, CancellationToken ct = default);
    Task<Result> DeleteAsync(DeleteKeycloakIntegration command, CancellationToken ct = default);
    Task<Result<KeycloakConnectionTestResult>> ValidateAsync(ValidateKeycloakConnection command, CancellationToken ct = default);
    Task<Result<KeycloakConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default);
    Task<Result<KeycloakDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default);
}
