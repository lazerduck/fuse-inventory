using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class KumaIntegrationService : IKumaIntegrationService
{
    private readonly IFuseStore _store;
    private readonly IKumaIntegrationValidator _validator;

    public KumaIntegrationService(IFuseStore store, IKumaIntegrationValidator validator)
    {
        _store = store;
        _validator = validator;
    }

    public async Task<IReadOnlyList<KumaIntegration>> GetKumaIntegrationsAsync() => (await _store.GetAsync()).KumaIntegrations;

    public async Task<KumaIntegration?> GetKumaIntegrationByIdAsync(Guid id) => (await _store.GetAsync()).KumaIntegrations.FirstOrDefault(k => k.Id == id);

    public async Task<Result<KumaIntegration>> CreateKumaIntegrationAsync(CreateKumaIntegration command, CancellationToken ct = default)
    {
        if (command.EnvironmentIds is null || command.EnvironmentIds.Count == 0)
            return Result<KumaIntegration>.Failure("At least one environment must be specified.", ErrorType.Validation);

        var snapshot = await _store.GetAsync(ct);

        // Validate environments exist
        foreach (var envId in command.EnvironmentIds)
        {
            if (!snapshot.Environments.Any(e => e.Id == envId))
                return Result<KumaIntegration>.Failure($"Environment {envId} not found.", ErrorType.Validation);
        }

        // Optional platform and account existence
        if (command.PlatformId is Guid pid && !snapshot.Platforms.Any(p => p.Id == pid))
            return Result<KumaIntegration>.Failure($"Platform {pid} not found.", ErrorType.Validation);
        if (command.AccountId is Guid aid && !snapshot.Accounts.Any(a => a.Id == aid))
            return Result<KumaIntegration>.Failure($"Account {aid} not found.", ErrorType.Validation);

        // Validate endpoint (async external check)
        var isValid = await _validator.ValidateAsync(command.Uri, command.ApiKey, ct);
        if (!isValid)
            return Result<KumaIntegration>.Failure("Failed to validate Kuma integration (URI/API key invalid).", ErrorType.Validation);

        var now = DateTime.UtcNow;
        var integration = new KumaIntegration(
            Id: Guid.NewGuid(),
            Name: command.Name ?? command.Uri.Host,
            EnvironmentIds: command.EnvironmentIds.AsReadOnly(),
            PlatformId: command.PlatformId,
            AccountId: command.AccountId,
            Uri: command.Uri,
            ApiKey: command.ApiKey,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _store.UpdateAsync(s => s with { KumaIntegrations = s.KumaIntegrations.Append(integration).ToList() }, ct);
        return Result<KumaIntegration>.Success(integration);
    }

    public async Task<Result<KumaIntegration>> UpdateKumaIntegrationAsync(UpdateKumaIntegration command, CancellationToken ct = default)
    {
        var snapshot = await _store.GetAsync(ct);
        var existing = snapshot.KumaIntegrations.FirstOrDefault(k => k.Id == command.Id);
        if (existing is null)
            return Result<KumaIntegration>.Failure($"Kuma integration {command.Id} not found.", ErrorType.NotFound);

        if (command.EnvironmentIds is null || command.EnvironmentIds.Count == 0)
            return Result<KumaIntegration>.Failure("At least one environment must be specified.", ErrorType.Validation);

        foreach (var envId in command.EnvironmentIds)
        {
            if (!snapshot.Environments.Any(e => e.Id == envId))
                return Result<KumaIntegration>.Failure($"Environment {envId} not found.", ErrorType.Validation);
        }
        if (command.PlatformId is Guid pid && !snapshot.Platforms.Any(p => p.Id == pid))
            return Result<KumaIntegration>.Failure($"Platform {pid} not found.", ErrorType.Validation);
        if (command.AccountId is Guid aid && !snapshot.Accounts.Any(a => a.Id == aid))
            return Result<KumaIntegration>.Failure($"Account {aid} not found.", ErrorType.Validation);

        bool needsValidation = existing.Uri != command.Uri || existing.ApiKey != command.ApiKey;
        if (needsValidation)
        {
            var isValid = await _validator.ValidateAsync(command.Uri, command.ApiKey, ct);
            if (!isValid)
                return Result<KumaIntegration>.Failure("Failed to validate Kuma integration (URI/API key invalid).", ErrorType.Validation);
        }

        var updated = existing with
        {
            Name = command.Name ?? existing.Name,
            EnvironmentIds = command.EnvironmentIds.AsReadOnly(),
            PlatformId = command.PlatformId,
            AccountId = command.AccountId,
            Uri = command.Uri,
            ApiKey = command.ApiKey,
            UpdatedAt = DateTime.UtcNow
        };

        await _store.UpdateAsync(s => s with
        {
            KumaIntegrations = s.KumaIntegrations.Select(k => k.Id == existing.Id ? updated : k).ToList()
        }, ct);
        return Result<KumaIntegration>.Success(updated);
    }

    public async Task<Result> DeleteKumaIntegrationAsync(DeleteKumaIntegration command)
    {
        var snapshot = await _store.GetAsync();
        if (!snapshot.KumaIntegrations.Any(k => k.Id == command.Id))
            return Result.Failure($"Kuma integration {command.Id} not found.", ErrorType.NotFound);

        await _store.UpdateAsync(s => s with
        {
            KumaIntegrations = s.KumaIntegrations.Where(k => k.Id != command.Id).ToList()
        });
        return Result.Success();
    }
}
