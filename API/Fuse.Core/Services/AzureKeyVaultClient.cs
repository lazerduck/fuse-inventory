using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class AzureKeyVaultClient : IAzureKeyVaultClient
{
    public async Task<Result> TestConnectionAsync(Uri vaultUri, SecretProviderAuthMode authMode, SecretProviderCredentials? credentials)
    {
        try
        {
            var credential = GetCredential(authMode, credentials);
            var client = new SecretClient(vaultUri, credential);
            
            // Test by attempting to list secrets (doesn't return actual values, just metadata)
            var response = client.GetPropertiesOfSecretsAsync();
            await response.AsPages(pageSizeHint: 1).FirstOrDefaultAsync();
            
            return Result.Success();
        }
        catch (AuthenticationFailedException ex)
        {
            return Result.Failure($"Authentication failed: {ex.Message}", ErrorType.Validation);
        }
        catch (RequestFailedException ex)
        {
            return Result.Failure($"Azure Key Vault error: {ex.Message}", ErrorType.Validation);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Connection test failed: {ex.Message}", ErrorType.Validation);
        }
    }

    public async Task<Result> CheckSecretExistsAsync(SecretProvider provider, string secretName)
    {
        try
        {
            var client = GetClient(provider);
            var response = await client.GetSecretAsync(secretName);
            return Result.Success();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return Result.Failure($"Secret '{secretName}' not found.", ErrorType.NotFound);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to check secret: {ex.Message}", ErrorType.Validation);
        }
    }

    public async Task<Result> CreateSecretAsync(SecretProvider provider, string secretName, string secretValue)
    {
        try
        {
            var client = GetClient(provider);
            await client.SetSecretAsync(secretName, secretValue);
            return Result.Success();
        }
        catch (RequestFailedException ex)
        {
            return Result.Failure($"Failed to create secret: {ex.Message}", ErrorType.Validation);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to create secret: {ex.Message}", ErrorType.Validation);
        }
    }

    public async Task<Result> RotateSecretAsync(SecretProvider provider, string secretName, string newSecretValue)
    {
        try
        {
            var client = GetClient(provider);
            // Setting a new value creates a new version
            await client.SetSecretAsync(secretName, newSecretValue);
            return Result.Success();
        }
        catch (RequestFailedException ex)
        {
            return Result.Failure($"Failed to rotate secret: {ex.Message}", ErrorType.Validation);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to rotate secret: {ex.Message}", ErrorType.Validation);
        }
    }

    public async Task<Result<string>> ReadSecretAsync(SecretProvider provider, string secretName, string? version = null)
    {
        try
        {
            var client = GetClient(provider);
            KeyVaultSecret secret;
            
            if (!string.IsNullOrEmpty(version))
            {
                // Get specific version
                secret = await client.GetSecretAsync(secretName, version);
            }
            else
            {
                // Get latest version
                secret = await client.GetSecretAsync(secretName);
            }
            
            return Result<string>.Success(secret.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return Result<string>.Failure($"Secret '{secretName}' not found.", ErrorType.NotFound);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to read secret: {ex.Message}", ErrorType.Validation);
        }
    }

    private SecretClient GetClient(SecretProvider provider)
    {
        var credential = GetCredential(provider.AuthMode, provider.Credentials);
        return new SecretClient(provider.VaultUri, credential);
    }

    private TokenCredential GetCredential(SecretProviderAuthMode authMode, SecretProviderCredentials? credentials)
    {
        return authMode switch
        {
            SecretProviderAuthMode.ManagedIdentity => new DefaultAzureCredential(),
            SecretProviderAuthMode.ClientSecret => CreateClientSecretCredential(credentials),
            _ => throw new ArgumentException($"Unsupported auth mode: {authMode}")
        };
    }

    private TokenCredential CreateClientSecretCredential(SecretProviderCredentials? credentials)
    {
        if (credentials is null || 
            string.IsNullOrWhiteSpace(credentials.TenantId) ||
            string.IsNullOrWhiteSpace(credentials.ClientId) ||
            string.IsNullOrWhiteSpace(credentials.ClientSecret))
        {
            throw new ArgumentException("Client secret credentials require TenantId, ClientId, and ClientSecret.");
        }

        return new ClientSecretCredential(
            credentials.TenantId,
            credentials.ClientId,
            credentials.ClientSecret
        );
    }
}
