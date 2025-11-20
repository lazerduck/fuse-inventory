using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using System.Collections.Generic;

namespace Fuse.Core.Services;

public class AzureKeyVaultClient : IAzureKeyVaultClient
{
    private readonly ITokenCredentialFactory _credentialFactory;
    private readonly IKeyVaultSecretClientFactory _secretClientFactory;

    public AzureKeyVaultClient(
        ITokenCredentialFactory? credentialFactory = null,
        IKeyVaultSecretClientFactory? secretClientFactory = null)
    {
        _credentialFactory = credentialFactory ?? new DefaultTokenCredentialFactory();
        _secretClientFactory = secretClientFactory ?? new DefaultKeyVaultSecretClientFactory();
    }

    public async Task<Result> TestConnectionAsync(Uri vaultUri, SecretProviderAuthMode authMode, SecretProviderCredentials? credentials)
    {
        try
        {
            var credential = _credentialFactory.Create(authMode, credentials);
            var client = _secretClientFactory.Create(vaultUri, credential);
            
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
                var resp = await client.GetSecretVersionAsync(secretName, version);
                secret = resp.Value;
            }
            else
            {
                var resp = await client.GetSecretAsync(secretName);
                secret = resp.Value;
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

    public async Task<Result<IReadOnlyList<SecretMetadata>>> ListSecretsAsync(SecretProvider provider)
    {
        try
        {
            var client = GetClient(provider);
            var items = new List<SecretMetadata>();

            await foreach (var secretProps in client.GetPropertiesOfSecretsAsync())
            {
                items.Add(new SecretMetadata(
                    Name: secretProps.Name,
                    Enabled: secretProps.Enabled ?? false,
                    UpdatedOn: secretProps.UpdatedOn,
                    ContentType: secretProps.ContentType
                ));
            }

            return Result<IReadOnlyList<SecretMetadata>>.Success(items);
        }
        catch (RequestFailedException ex)
        {
            return Result<IReadOnlyList<SecretMetadata>>.Failure($"Failed to list secrets: {ex.Message}", ErrorType.Validation);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<SecretMetadata>>.Failure($"Failed to list secrets: {ex.Message}");
        }
    }

    private IKeyVaultSecretClient GetClient(SecretProvider provider)
    {
        var credential = _credentialFactory.Create(provider.AuthMode, provider.Credentials);
        return _secretClientFactory.Create(provider.VaultUri, credential);
    }
}

// Default factories (internal) used when not injected
internal sealed class DefaultTokenCredentialFactory : ITokenCredentialFactory
{
    public TokenCredential Create(SecretProviderAuthMode mode, SecretProviderCredentials? credentials) => mode switch
    {
        SecretProviderAuthMode.ManagedIdentity => new DefaultAzureCredential(),
        SecretProviderAuthMode.ClientSecret => CreateClientSecretCredential(credentials),
        _ => throw new ArgumentException($"Unsupported auth mode: {mode}")
    };

    private static TokenCredential CreateClientSecretCredential(SecretProviderCredentials? credentials)
    {
        if (credentials is null ||
            string.IsNullOrWhiteSpace(credentials.TenantId) ||
            string.IsNullOrWhiteSpace(credentials.ClientId) ||
            string.IsNullOrWhiteSpace(credentials.ClientSecret))
        {
            throw new ArgumentException("Client secret credentials require TenantId, ClientId, and ClientSecret.");
        }
        return new ClientSecretCredential(credentials.TenantId, credentials.ClientId, credentials.ClientSecret);
    }
}

internal sealed class DefaultKeyVaultSecretClientFactory : IKeyVaultSecretClientFactory
{
    public IKeyVaultSecretClient Create(Uri vaultUri, TokenCredential credential) => new KeyVaultSecretClientWrapper(new SecretClient(vaultUri, credential));
}

internal sealed class KeyVaultSecretClientWrapper : IKeyVaultSecretClient
{
    private readonly SecretClient _inner;
    public KeyVaultSecretClientWrapper(SecretClient inner) => _inner = inner;
    public Task<Response<KeyVaultSecret>> GetSecretAsync(string name, CancellationToken ct = default) => _inner.GetSecretAsync(name, cancellationToken: ct);
    public Task<Response<KeyVaultSecret>> GetSecretVersionAsync(string name, string version, CancellationToken ct = default) => _inner.GetSecretAsync(name, version, ct);
    public Task<Response<KeyVaultSecret>> SetSecretAsync(string name, string value, CancellationToken ct = default) => _inner.SetSecretAsync(name, value, ct);
    public AsyncPageable<SecretProperties> GetPropertiesOfSecretsAsync(CancellationToken ct = default) => _inner.GetPropertiesOfSecretsAsync(ct);
}
