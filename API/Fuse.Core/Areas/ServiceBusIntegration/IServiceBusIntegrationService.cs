using Fuse.Core.Models;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.ServiceBusIntegration;

public interface IServiceBusIntegrationService
{
    Task<IReadOnlyList<ServiceBusConnection>> GetAllAsync(CancellationToken ct = default);
    Task<ServiceBusConnection?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<ServiceBusConnection>> CreateAsync(CreateServiceBusIntegration command, CancellationToken ct = default);
    Task<Result<ServiceBusConnection>> UpdateAsync(UpdateServiceBusIntegration command, CancellationToken ct = default);
    Task<Result> DeleteAsync(DeleteServiceBusIntegration command, CancellationToken ct = default);
    Task<Result<ServiceBusConnectionTestResult>> ValidateAsync(ValidateServiceBusConnection command, CancellationToken ct = default);
    Task<Result<ServiceBusConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default);
    Task<Result<ServiceBusDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default);
}
