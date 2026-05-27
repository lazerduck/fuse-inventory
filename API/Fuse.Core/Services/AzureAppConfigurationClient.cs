using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Data.AppConfiguration;
using Azure.Identity;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Core.Services;

public class AzureAppConfigurationClient : IAzureAppConfigurationClient
{
    private const string KeyVaultReferenceContentType = "application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8";

    private readonly ITokenCredentialFactory _credentialFactory;
    private readonly IAppConfigurationClientFactory _clientFactory;
    private readonly IAzureIntegrationManagerService? _azureIntegrationManagerService;

    public AzureAppConfigurationClient(
        ITokenCredentialFactory? credentialFactory = null,
        IAppConfigurationClientFactory? clientFactory = null,
        IAzureIntegrationManagerService? azureIntegrationManagerService = null)
    {
        _credentialFactory = credentialFactory ?? new DefaultTokenCredentialFactory();
        _clientFactory = clientFactory ?? new DefaultAppConfigurationClientFactory();
        _azureIntegrationManagerService = azureIntegrationManagerService;
    }

    public async Task<Result> TestConnectionAsync(Uri endpoint, SecretProviderAuthMode authMode, SecretProviderCredentials? credentials)
    {
        try
        {
            var credential = _credentialFactory.Create(authMode, credentials);
            var client = _clientFactory.Create(endpoint, credential);
            var settings = client.GetConfigurationSettingsAsync();
            await settings.AsPages(pageSizeHint: 1).FirstOrDefaultAsync();
            return Result.Success();
        }
        catch (AuthenticationFailedException ex)
        {
            return Result.Failure($"Authentication failed: {ex.Message}", ErrorType.Validation);
        }
        catch (RequestFailedException ex)
        {
            return Result.Failure($"Azure App Configuration error: {ex.Message}", ErrorType.Validation);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Connection test failed: {ex.Message}", ErrorType.Validation);
        }
    }

    public async Task<Result<IReadOnlyList<AppConfigurationEntry>>> ListKeyValuesAsync(
        SecretProvider provider,
        string? keySearch = null,
        string? keyPrefix = null,
        string? label = null)
    {
        try
        {
            var client = await GetClientAsync(provider);
            var items = new List<AppConfigurationEntry>();

            var normalizedPrefix = keyPrefix?.Trim();
            var prefixFilter = string.IsNullOrWhiteSpace(normalizedPrefix) ? "*" : $"{normalizedPrefix}*";
            var labelFilter = string.IsNullOrWhiteSpace(label) ? "*" : label.Trim();
            var normalizedSearch = keySearch?.Trim();

            await foreach (var setting in client.GetConfigurationSettingsAsync(prefixFilter, labelFilter))
            {
                if (!string.IsNullOrWhiteSpace(normalizedSearch)
                    && !setting.Key.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isKeyVaultReference = IsKeyVaultReference(setting);
                items.Add(new AppConfigurationEntry(
                    Key: setting.Key,
                    Value: isKeyVaultReference ? null : setting.Value,
                    Label: setting.Label,
                    ContentType: setting.ContentType,
                    LastModified: setting.LastModified,
                    IsLocked: setting.IsReadOnly ?? false,
                    IsKeyVaultReference: isKeyVaultReference,
                    KeyVaultReferenceUri: isKeyVaultReference ? TryExtractKeyVaultReferenceUri(setting.Value) : null
                ));
            }

            return Result<IReadOnlyList<AppConfigurationEntry>>.Success(items);
        }
        catch (RequestFailedException ex)
        {
            return Result<IReadOnlyList<AppConfigurationEntry>>.Failure($"Failed to list App Configuration key-values: {ex.Message}", ErrorType.Validation);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AppConfigurationEntry>>.Failure($"Failed to list App Configuration key-values: {ex.Message}", ErrorType.Validation);
        }
    }

    private async Task<IAppConfigurationClient> GetClientAsync(SecretProvider provider)
    {
        var credentials = await ResolveProviderCredentialsAsync(provider);
        var credential = _credentialFactory.Create(provider.AuthMode, credentials);
        return _clientFactory.Create(provider.VaultUri, credential);
    }

    private async Task<SecretProviderCredentials?> ResolveProviderCredentialsAsync(SecretProvider provider)
    {
        if (provider.AuthMode != SecretProviderAuthMode.ClientSecret)
            return provider.Credentials;

        var sharedCredentials = _azureIntegrationManagerService is null
            ? null
            : await _azureIntegrationManagerService.GetClientSecretCredentialsAsync();
        return HasCompleteClientSecretCredentials(sharedCredentials)
            ? sharedCredentials
            : provider.Credentials;
    }

    private static bool HasCompleteClientSecretCredentials(SecretProviderCredentials? credentials)
        => credentials is not null
           && !string.IsNullOrWhiteSpace(credentials.TenantId)
           && !string.IsNullOrWhiteSpace(credentials.ClientId)
           && !string.IsNullOrWhiteSpace(credentials.ClientSecret);

    private static bool IsKeyVaultReference(ConfigurationSetting setting)
        => setting.ContentType?.Equals(KeyVaultReferenceContentType, StringComparison.OrdinalIgnoreCase) == true;

    private static string? TryExtractKeyVaultReferenceUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.TryGetProperty("uri", out var uriProperty)
                ? uriProperty.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal sealed class DefaultAppConfigurationClientFactory : IAppConfigurationClientFactory
{
    public IAppConfigurationClient Create(Uri endpoint, TokenCredential credential)
        => new AppConfigurationClientWrapper(new ConfigurationClient(endpoint, credential));
}

internal sealed class AppConfigurationClientWrapper : IAppConfigurationClient
{
    private readonly ConfigurationClient _inner;

    public AppConfigurationClientWrapper(ConfigurationClient inner) => _inner = inner;

    public AsyncPageable<ConfigurationSetting> GetConfigurationSettingsAsync(string keyFilter = "*", string labelFilter = "*", CancellationToken ct = default)
    {
        var selector = new SettingSelector
        {
            KeyFilter = keyFilter,
            LabelFilter = labelFilter
        };
        return _inner.GetConfigurationSettingsAsync(selector, ct);
    }
}
