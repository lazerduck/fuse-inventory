using Fuse.Core.Areas.Audit;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Responses;

namespace Fuse.Core.Areas.ServiceBusIntegration;

public sealed class ServiceBusIntegrationService(IFuseStore store, ServiceBusInspector inspector, IAuditService audit,
    ICurrentUser currentUser) : IServiceBusIntegrationService
{
    private const string Missing = "Service Bus integration not found.";

    public async Task<IReadOnlyList<ServiceBusConnection>> GetAllAsync(CancellationToken ct = default) =>
        (await store.GetAsync(ct)).ServiceBusIntegrations;
    public async Task<ServiceBusConnection?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await GetAllAsync(ct)).FirstOrDefault(i => i.Id == id);
    public Task<Result<ServiceBusConnection>> CreateAsync(CreateServiceBusIntegration command, CancellationToken ct = default) =>
        Save(null, command.Name, command.MessageBrokerId, command.NamespaceResourceId, command.AuthMode, command.CredentialProfileId, false, ct);
    public Task<Result<ServiceBusConnection>> UpdateAsync(UpdateServiceBusIntegration command, CancellationToken ct = default) =>
        Save(command.Id, command.Name, command.MessageBrokerId, command.NamespaceResourceId, command.AuthMode, command.CredentialProfileId, command.UseSharedCredential, ct);

    private async Task<Result<ServiceBusConnection>> Save(Guid? id, string name, Guid brokerId, string resourceId, SecretProviderAuthMode mode, Guid? profileId, bool useShared, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || !ServiceBusInspector.Valid(resourceId, mode))
            return Result<ServiceBusConnection>.Failure("Enter a name, an Azure Service Bus namespace resource ID and a valid authentication mode.");
        var result = Result<ServiceBusConnection>.Failure(Missing, ErrorType.NotFound);
        await store.UpdateAsync(snapshot =>
        {
            var existing = snapshot.ServiceBusIntegrations.FirstOrDefault(i => i.Id == id);
            if (id.HasValue && existing is null) return snapshot;
            var selectedProfile = profileId ?? (useShared || mode != SecretProviderAuthMode.ClientSecret ? null : existing?.CredentialProfileId);
            if (!AzureCredentialProfile.Allows(snapshot.AzureIntegrationManager, selectedProfile, resourceId, mode))
            {
                result = Result<ServiceBusConnection>.Failure("Select a credential profile for this Azure resource.");
                return snapshot;
            }
            if (!snapshot.MessageBrokers.Any(b => b.Id == brokerId && string.Equals(b.Kind, "Azure Service Bus", StringComparison.OrdinalIgnoreCase)))
            {
                result = Result<ServiceBusConnection>.Failure("Select an Azure Service Bus message broker.");
                return snapshot;
            }
            if (snapshot.ServiceBusIntegrations.Any(i => i.Id != id && i.MessageBrokerId == brokerId))
            {
                result = Result<ServiceBusConnection>.Failure("The broker already has a Service Bus integration.", ErrorType.Conflict);
                return snapshot;
            }
            var now = DateTime.UtcNow;
            var item = new ServiceBusConnection(id ?? Guid.NewGuid(), name.Trim(), brokerId, resourceId.TrimEnd('/'), mode, existing?.CreatedAt ?? now, now, selectedProfile);
            result = Result<ServiceBusConnection>.Success(item);
            return snapshot with { ServiceBusIntegrations = existing is null ? snapshot.ServiceBusIntegrations.Append(item).ToArray()
                : snapshot.ServiceBusIntegrations.Select(i => i.Id == id ? item : i).ToArray() };
        }, ct);
        if (result.IsSuccess) await Log(id.HasValue ? AuditAction.ServiceBusIntegrationUpdated : AuditAction.ServiceBusIntegrationCreated, result.Value!, ct);
        return result;
    }

    public async Task<Result> DeleteAsync(DeleteServiceBusIntegration command, CancellationToken ct = default)
    {
        ServiceBusConnection? removed = null;
        await store.UpdateAsync(snapshot =>
        {
            removed = snapshot.ServiceBusIntegrations.FirstOrDefault(i => i.Id == command.Id);
            return removed is null ? snapshot : snapshot with { ServiceBusIntegrations = snapshot.ServiceBusIntegrations.Where(i => i.Id != command.Id).ToArray() };
        }, ct);
        if (removed is null) return Result.Failure(Missing, ErrorType.NotFound);
        await Log(AuditAction.ServiceBusIntegrationDeleted, removed, ct);
        return Result.Success();
    }

    private Task Log(AuditAction action, ServiceBusConnection item, CancellationToken ct) =>
        audit.LogAsync(AuditHelper.CreateLog(action, AuditArea.ServiceBusIntegration, currentUser.UserName, currentUser.UserId, item.Id, item), ct);

    public async Task<Result<ServiceBusConnectionTestResult>> ValidateAsync(ValidateServiceBusConnection command, CancellationToken ct = default) =>
        ServiceBusInspector.Valid(command.NamespaceResourceId, command.AuthMode)
            ? Result<ServiceBusConnectionTestResult>.Success(await inspector.ValidateAsync(command.NamespaceResourceId.TrimEnd('/'), command.AuthMode, ct, command.CredentialProfileId))
            : Result<ServiceBusConnectionTestResult>.Failure("Enter a valid namespace resource ID and authentication mode.");

    public async Task<Result<ServiceBusConnectionTestResult>> ValidateSavedAsync(Guid id, CancellationToken ct = default) =>
        await GetByIdAsync(id, ct) is { } item ? await ValidateAsync(new(item.NamespaceResourceId, item.AuthMode, item.CredentialProfileId), ct)
            : Result<ServiceBusConnectionTestResult>.Failure(Missing, ErrorType.NotFound);

    public async Task<Result<ServiceBusDiscoveryResponse>> DiscoverAsync(Guid id, CancellationToken ct = default)
    {
        var snapshot = await store.GetAsync(ct);
        var item = snapshot.ServiceBusIntegrations.FirstOrDefault(i => i.Id == id);
        if (item is null) return Result<ServiceBusDiscoveryResponse>.Failure(Missing, ErrorType.NotFound);
        if (!ServiceBusInspector.Valid(item.NamespaceResourceId, item.AuthMode)) return Result<ServiceBusDiscoveryResponse>.Failure("Invalid saved namespace.");
        var broker = snapshot.MessageBrokers.FirstOrDefault(b => b.Id == item.MessageBrokerId);
        if (broker is null) return Result<ServiceBusDiscoveryResponse>.Failure("Message broker not found.", ErrorType.NotFound);
        return Result<ServiceBusDiscoveryResponse>.Success(await inspector.DiscoverAsync(item, broker.Queues ?? [], ct));
    }
}
