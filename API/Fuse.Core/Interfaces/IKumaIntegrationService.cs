using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Interfaces;

public interface IKumaIntegrationService
{
    Task<IReadOnlyList<KumaIntegrationResponse>> GetKumaIntegrationsAsync();
    Task<KumaIntegrationResponse?> GetKumaIntegrationByIdAsync(Guid id);
    Task<Result<KumaIntegrationResponse>> CreateKumaIntegrationAsync(CreateKumaIntegration command, CancellationToken ct = default);
    Task<Result<KumaIntegrationResponse>> UpdateKumaIntegrationAsync(UpdateKumaIntegration command, CancellationToken ct = default);
    Task<Result> DeleteKumaIntegrationAsync(DeleteKumaIntegration command);
}
