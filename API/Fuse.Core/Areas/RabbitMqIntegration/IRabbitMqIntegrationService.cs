using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.RabbitMqIntegration;

public interface IRabbitMqIntegrationService
{
    Task<IReadOnlyList<RabbitMqIntegrationResponse>> GetAllAsync(CancellationToken ct = default);
    Task<RabbitMqIntegrationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<RabbitMqIntegrationResponse>> CreateAsync(CreateRabbitMqIntegration command, CancellationToken ct = default);
    Task<Result<RabbitMqIntegrationResponse>> UpdateAsync(UpdateRabbitMqIntegration command, CancellationToken ct = default);
    Task<Result> DeleteAsync(DeleteRabbitMqIntegration command, CancellationToken ct = default);
    Task<Result<RabbitMqConnectionTestResult>> ValidateAsync(ValidateRabbitMqConnection command, CancellationToken ct = default);
    Task<Result<RabbitMqConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default);
    Task<Result<RabbitMqDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default);
}
