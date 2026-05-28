using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using System.Text.Json;

namespace Fuse.Core.Services;

public class AppConfigurationOperationService : IAppConfigurationOperationService
{
    private readonly IFuseStore _fuseStore;
    private readonly IAzureAppConfigurationClient _azureAppConfigurationClient;
    private readonly IAuditService _auditService;

    public AppConfigurationOperationService(
        IFuseStore fuseStore,
        IAzureAppConfigurationClient azureAppConfigurationClient,
        IAuditService auditService)
    {
        _fuseStore = fuseStore;
        _azureAppConfigurationClient = azureAppConfigurationClient;
        _auditService = auditService;
    }

    public async Task<Result<IReadOnlyList<AppConfigurationEntry>>> ListKeyValuesAsync(
        Guid providerId,
        string? keySearch = null,
        string? keyPrefix = null,
        string? label = null)
    {
        var store = await _fuseStore.GetAsync();
        var provider = store.SecretProviders.FirstOrDefault(p => p.Id == providerId);

        if (provider is null)
            return Result<IReadOnlyList<AppConfigurationEntry>>.Failure($"Secret provider with ID '{providerId}' not found.", ErrorType.NotFound);

        if (!provider.Capabilities.HasFlag(SecretProviderCapabilities.Check))
            return Result<IReadOnlyList<AppConfigurationEntry>>.Failure("This integration does not have Check capability enabled.", ErrorType.Validation);

        if (!SecretProviderEndpointClassifier.IsAppConfigurationEndpoint(provider.VaultUri))
            return Result<IReadOnlyList<AppConfigurationEntry>>.Failure("This integration is not an Azure App Configuration endpoint.", ErrorType.Validation);

        return await _azureAppConfigurationClient.ListKeyValuesAsync(provider, keySearch, keyPrefix, label);
    }

    public async Task<Result<AppConfigurationEntry>> SetKeyValueAsync(SetAppConfigurationValue command, string userName, Guid? userId)
    {
        if (string.IsNullOrWhiteSpace(command.Key))
            return Result<AppConfigurationEntry>.Failure("Key is required.", ErrorType.Validation);

        if (command.Value is null)
            return Result<AppConfigurationEntry>.Failure("Value is required.", ErrorType.Validation);

        var store = await _fuseStore.GetAsync();
        var provider = store.SecretProviders.FirstOrDefault(p => p.Id == command.ProviderId);

        if (provider is null)
            return Result<AppConfigurationEntry>.Failure($"Secret provider with ID '{command.ProviderId}' not found.", ErrorType.NotFound);

        if (!SecretProviderEndpointClassifier.IsAppConfigurationEndpoint(provider.VaultUri))
            return Result<AppConfigurationEntry>.Failure("This integration is not an Azure App Configuration endpoint.", ErrorType.Validation);

        // Fetch the existing entry to check lock status and capture old value for audit
        var existingResult = await _azureAppConfigurationClient.GetKeyValueAsync(provider, command.Key, command.Label);
        if (!existingResult.IsSuccess)
            return Result<AppConfigurationEntry>.Failure(existingResult.Error!, existingResult);

        var existing = existingResult.Value;
        var isCreate = existing is null;

        if (!isCreate)
        {
            // Prevent editing locked entries
            if (existing!.IsLocked)
                return Result<AppConfigurationEntry>.Failure(
                    $"The key '{command.Key}' is locked and cannot be edited. Unlock it in Azure App Configuration first.",
                    ErrorType.Validation);

            // Prevent editing Key Vault references
            if (existing.IsKeyVaultReference)
                return Result<AppConfigurationEntry>.Failure(
                    $"The key '{command.Key}' is a Key Vault reference and cannot be edited directly.",
                    ErrorType.Validation);

            if (!provider.Capabilities.HasFlag(SecretProviderCapabilities.Rotate))
                return Result<AppConfigurationEntry>.Failure("This integration does not have Rotate capability enabled.", ErrorType.Validation);
        }
        else
        {
            if (!provider.Capabilities.HasFlag(SecretProviderCapabilities.Create))
                return Result<AppConfigurationEntry>.Failure("This integration does not have Create capability enabled.", ErrorType.Validation);
        }

        // Preserve content type from existing entry when not explicitly provided
        var effectiveContentType = command.ContentType ?? existing?.ContentType;

        var setResult = await _azureAppConfigurationClient.SetKeyValueAsync(
            provider, command.Key, command.Label, command.Value, effectiveContentType);

        if (!setResult.IsSuccess)
            return setResult;

        var auditAction = isCreate ? AuditAction.AppConfigurationKeyValueCreated : AuditAction.AppConfigurationKeyValueUpdated;
        var auditDetails = new
        {
            ProviderId = provider.Id,
            ProviderName = provider.Name,
            Key = command.Key,
            Label = command.Label,
            OldValue = isCreate ? null : existing?.Value,
            NewValue = command.Value,
            ContentType = effectiveContentType
        };

        await _auditService.LogAsync(new AuditLog(
            id: Guid.NewGuid(),
            timestamp: DateTime.UtcNow,
            action: auditAction,
            area: AuditArea.SecretProvider,
            userName: userName,
            userId: userId,
            entityId: provider.Id,
            changeDetails: JsonSerializer.Serialize(auditDetails)
        ));

        return setResult;
    }
}
