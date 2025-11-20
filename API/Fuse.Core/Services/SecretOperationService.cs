using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using System.Collections.Generic;

namespace Fuse.Core.Services;

public class SecretOperationService : ISecretOperationService
{
    private readonly IFuseStore _fuseStore;
    private readonly IAzureKeyVaultClient _azureKeyVaultClient;
    private readonly IAuditService _auditService;

    public SecretOperationService(
        IFuseStore fuseStore, 
        IAzureKeyVaultClient azureKeyVaultClient,
        IAuditService auditService)
    {
        _fuseStore = fuseStore;
        _azureKeyVaultClient = azureKeyVaultClient;
        _auditService = auditService;
    }

    public async Task<Result<IReadOnlyList<SecretMetadata>>> ListSecretsAsync(Guid providerId)
    {
        var store = await _fuseStore.GetAsync();
        var provider = store.SecretProviders.FirstOrDefault(p => p.Id == providerId);

        if (provider is null)
            return Result<IReadOnlyList<SecretMetadata>>.Failure($"Secret provider with ID '{providerId}' not found.", ErrorType.NotFound);

        if (!provider.Capabilities.HasFlag(SecretProviderCapabilities.Check))
            return Result<IReadOnlyList<SecretMetadata>>.Failure("This secret provider does not have Check capability enabled.");

        return await _azureKeyVaultClient.ListSecretsAsync(provider);
    }

    public async Task<Result> CreateSecretAsync(CreateSecret command, string userName, Guid? userId)
    {
        var store = await _fuseStore.GetAsync();
        var provider = store.SecretProviders.FirstOrDefault(p => p.Id == command.ProviderId);
        
        if (provider is null)
            return Result.Failure($"Secret provider with ID '{command.ProviderId}' not found.", ErrorType.NotFound);

        if (!provider.Capabilities.HasFlag(SecretProviderCapabilities.Create))
            return Result.Failure("This secret provider does not have Create capability enabled.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(command.SecretName))
            return Result.Failure("Secret name is required.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(command.SecretValue))
            return Result.Failure("Secret value is required.", ErrorType.Validation);

        var result = await _azureKeyVaultClient.CreateSecretAsync(provider, command.SecretName, command.SecretValue);
        
        if (result.IsSuccess)
        {
            // Audit the operation
            await _auditService.LogAsync(new AuditLog(
                id: Guid.NewGuid(),
                timestamp: DateTime.UtcNow,
                action: AuditAction.SecretCreated,
                area: AuditArea.Secret,
                userName: userName,
                userId: userId,
                entityId: provider.Id,
                changeDetails: $"Secret '{command.SecretName}' created in provider '{provider.Name}'"
            ));
        }

        return result;
    }

    public async Task<Result> RotateSecretAsync(RotateSecret command, string userName, Guid? userId)
    {
        var store = await _fuseStore.GetAsync();
        var provider = store.SecretProviders.FirstOrDefault(p => p.Id == command.ProviderId);
        
        if (provider is null)
            return Result.Failure($"Secret provider with ID '{command.ProviderId}' not found.", ErrorType.NotFound);

        if (!provider.Capabilities.HasFlag(SecretProviderCapabilities.Rotate))
            return Result.Failure("This secret provider does not have Rotate capability enabled.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(command.SecretName))
            return Result.Failure("Secret name is required.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(command.NewSecretValue))
            return Result.Failure("New secret value is required.", ErrorType.Validation);

        var result = await _azureKeyVaultClient.RotateSecretAsync(provider, command.SecretName, command.NewSecretValue);
        
        if (result.IsSuccess)
        {
            // Audit the operation
            await _auditService.LogAsync(new AuditLog(
                id: Guid.NewGuid(),
                timestamp: DateTime.UtcNow,
                action: AuditAction.SecretRotated,
                area: AuditArea.Secret,
                userName: userName,
                userId: userId,
                entityId: provider.Id,
                changeDetails: $"Secret '{command.SecretName}' rotated in provider '{provider.Name}'"
            ));
        }

        return result;
    }

    public async Task<Result<string>> RevealSecretAsync(RevealSecret command, string userName, Guid? userId)
    {
        var store = await _fuseStore.GetAsync();
        var provider = store.SecretProviders.FirstOrDefault(p => p.Id == command.ProviderId);
        
        if (provider is null)
            return Result<string>.Failure($"Secret provider with ID '{command.ProviderId}' not found.", ErrorType.NotFound);

        if (!provider.Capabilities.HasFlag(SecretProviderCapabilities.Read))
            return Result<string>.Failure("This secret provider does not have Read capability enabled.", ErrorType.Validation);

        if (string.IsNullOrWhiteSpace(command.SecretName))
            return Result<string>.Failure("Secret name is required.", ErrorType.Validation);

        var result = await _azureKeyVaultClient.ReadSecretAsync(provider, command.SecretName, command.Version);
        
        if (result.IsSuccess)
        {
            // Audit the operation - IMPORTANT: secret operations must be audited
            await _auditService.LogAsync(new AuditLog(
                id: Guid.NewGuid(),
                timestamp: DateTime.UtcNow,
                action: AuditAction.SecretRevealed,
                area: AuditArea.Secret,
                userName: userName,
                userId: userId,
                entityId: provider.Id,
                changeDetails: $"Secret '{command.SecretName}' (version: {command.Version ?? "latest"}) revealed from provider '{provider.Name}'"
            ));
        }

        return result;
    }
}
