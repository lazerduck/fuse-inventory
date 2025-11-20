using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class SecretProviderService : ISecretProviderService
{
    private readonly IFuseStore _fuseStore;
    private readonly IAzureKeyVaultClient _azureKeyVaultClient;

    public SecretProviderService(IFuseStore fuseStore, IAzureKeyVaultClient azureKeyVaultClient)
    {
        _fuseStore = fuseStore;
        _azureKeyVaultClient = azureKeyVaultClient;
    }

    public async Task<IReadOnlyList<SecretProvider>> GetSecretProvidersAsync()
        => (await _fuseStore.GetAsync()).SecretProviders;

    public async Task<SecretProvider?> GetSecretProviderByIdAsync(Guid id)
        => (await _fuseStore.GetAsync()).SecretProviders.FirstOrDefault(p => p.Id == id);

    public async Task<Result<SecretProvider>> CreateSecretProviderAsync(CreateSecretProvider command)
    {
        var validation = ValidateSecretProviderCommand(command.Name, command.VaultUri, command.AuthMode, command.Credentials, command.Capabilities);
        if (!validation.IsSuccess)
            return Result<SecretProvider>.Failure(validation.Error!, validation.ErrorType ?? ErrorType.Validation);

        // Test connection before creating
        var testResult = await _azureKeyVaultClient.TestConnectionAsync(command.VaultUri, command.AuthMode, command.Credentials);
        if (!testResult.IsSuccess)
            return Result<SecretProvider>.Failure($"Connection test failed: {testResult.Error}", ErrorType.Validation);

        var now = DateTime.UtcNow;
        var provider = new SecretProvider(
            Id: Guid.NewGuid(),
            Name: command.Name,
            VaultUri: command.VaultUri,
            AuthMode: command.AuthMode,
            Credentials: command.Credentials,
            Capabilities: command.Capabilities,
            CreatedAt: now,
            UpdatedAt: now
        );

        await _fuseStore.UpdateAsync(s => s with { SecretProviders = s.SecretProviders.Append(provider).ToList() });
        return Result<SecretProvider>.Success(provider);
    }

    public async Task<Result<SecretProvider>> UpdateSecretProviderAsync(UpdateSecretProvider command)
    {
        var store = await _fuseStore.GetAsync();
        var existing = store.SecretProviders.FirstOrDefault(p => p.Id == command.Id);
        if (existing is null)
            return Result<SecretProvider>.Failure($"Secret provider with ID '{command.Id}' not found.", ErrorType.NotFound);

        var validation = ValidateSecretProviderCommand(command.Name, command.VaultUri, command.AuthMode, command.Credentials, command.Capabilities);
        if (!validation.IsSuccess)
            return Result<SecretProvider>.Failure(validation.Error!, validation.ErrorType ?? ErrorType.Validation);

        // Test connection if credentials or vault URI changed
        bool needsTest = existing.VaultUri != command.VaultUri || 
                        existing.AuthMode != command.AuthMode ||
                        !CredentialsEqual(existing.Credentials, command.Credentials);
        
        if (needsTest)
        {
            var testResult = await _azureKeyVaultClient.TestConnectionAsync(command.VaultUri, command.AuthMode, command.Credentials);
            if (!testResult.IsSuccess)
                return Result<SecretProvider>.Failure($"Connection test failed: {testResult.Error}", ErrorType.Validation);
        }

        var updated = existing with
        {
            Name = command.Name,
            VaultUri = command.VaultUri,
            AuthMode = command.AuthMode,
            Credentials = command.Credentials,
            Capabilities = command.Capabilities,
            UpdatedAt = DateTime.UtcNow
        };

        await _fuseStore.UpdateAsync(s => s with 
        { 
            SecretProviders = s.SecretProviders.Select(p => p.Id == command.Id ? updated : p).ToList() 
        });
        return Result<SecretProvider>.Success(updated);
    }

    public async Task<Result> DeleteSecretProviderAsync(DeleteSecretProvider command)
    {
        var store = await _fuseStore.GetAsync();
        if (!store.SecretProviders.Any(p => p.Id == command.Id))
            return Result.Failure($"Secret provider with ID '{command.Id}' not found.", ErrorType.NotFound);

        // Check if any accounts are using this provider
        var accountsUsingProvider = store.Accounts.Where(a => 
            a.SecretBinding.Kind == SecretBindingKind.AzureKeyVault &&
            a.SecretBinding.AzureKeyVault?.ProviderId == command.Id
        ).ToList();

        if (accountsUsingProvider.Any())
        {
            return Result.Failure(
                $"Cannot delete secret provider: {accountsUsingProvider.Count} account(s) are using it.", 
                ErrorType.Validation
            );
        }

        await _fuseStore.UpdateAsync(s => s with 
        { 
            SecretProviders = s.SecretProviders.Where(p => p.Id != command.Id).ToList() 
        });
        return Result.Success();
    }

    public async Task<Result> TestConnectionAsync(TestSecretProviderConnection command)
    {
        var validation = ValidateTestConnectionCommand(command.VaultUri, command.AuthMode, command.Credentials);
        if (!validation.IsSuccess)
            return validation;

        return await _azureKeyVaultClient.TestConnectionAsync(command.VaultUri, command.AuthMode, command.Credentials);
    }

    private Result ValidateSecretProviderCommand(string name, Uri vaultUri, SecretProviderAuthMode authMode, SecretProviderCredentials? credentials, SecretProviderCapabilities capabilities)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Name is required.", ErrorType.Validation);

        if (vaultUri is null)
            return Result.Failure("Vault URI is required.", ErrorType.Validation);

        // Check capability must always be enabled
        if (!capabilities.HasFlag(SecretProviderCapabilities.Check))
            return Result.Failure("Check capability is required and must be enabled.", ErrorType.Validation);

        if (authMode == SecretProviderAuthMode.ClientSecret)
        {
            if (credentials is null ||
                string.IsNullOrWhiteSpace(credentials.TenantId) ||
                string.IsNullOrWhiteSpace(credentials.ClientId) ||
                string.IsNullOrWhiteSpace(credentials.ClientSecret))
            {
                return Result.Failure("Client secret authentication requires TenantId, ClientId, and ClientSecret.", ErrorType.Validation);
            }
        }

        return Result.Success();
    }

    private Result ValidateTestConnectionCommand(Uri vaultUri, SecretProviderAuthMode authMode, SecretProviderCredentials? credentials)
    {
        if (vaultUri is null)
            return Result.Failure("Vault URI is required.", ErrorType.Validation);

        if (authMode == SecretProviderAuthMode.ClientSecret)
        {
            if (credentials is null ||
                string.IsNullOrWhiteSpace(credentials.TenantId) ||
                string.IsNullOrWhiteSpace(credentials.ClientId) ||
                string.IsNullOrWhiteSpace(credentials.ClientSecret))
            {
                return Result.Failure("Client secret authentication requires TenantId, ClientId, and ClientSecret.", ErrorType.Validation);
            }
        }

        return Result.Success();
    }

    private bool CredentialsEqual(SecretProviderCredentials? a, SecretProviderCredentials? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.TenantId == b.TenantId && a.ClientId == b.ClientId && a.ClientSecret == b.ClientSecret;
    }
}
