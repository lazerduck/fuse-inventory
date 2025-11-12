using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface IKumaIntegrationService
{
    Task<IReadOnlyList<KumaIntegration>> GetKumaIntegrationsAsync();
    Task<KumaIntegration?> GetKumaIntegrationByIdAsync(Guid id);
    Task<Result<KumaIntegration>> CreateKumaIntegrationAsync(CreateKumaIntegration command, CancellationToken ct = default);
    Task<Result<KumaIntegration>> UpdateKumaIntegrationAsync(UpdateKumaIntegration command, CancellationToken ct = default);
    Task<Result> DeleteKumaIntegrationAsync(DeleteKumaIntegration command);
}
