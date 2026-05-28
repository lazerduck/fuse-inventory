using Azure;
using Azure.Core;
using Azure.Data.AppConfiguration;
using Azure.Identity;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Xunit;

namespace Fuse.Tests.Services;

public class AzureAppConfigurationClientTests
{
    private sealed class FakeCredentialFactory : ITokenCredentialFactory
    {
        public bool ThrowForClientSecret { get; set; }

        public TokenCredential Create(SecretProviderAuthMode mode, SecretProviderCredentials? credentials)
        {
            return mode switch
            {
                SecretProviderAuthMode.ManagedIdentity => new DummyCredential(),
                SecretProviderAuthMode.ClientSecret => HandleClientSecret(credentials),
                _ => throw new ArgumentException($"Unsupported auth mode: {mode}")
            };
        }

        private TokenCredential HandleClientSecret(SecretProviderCredentials? credentials)
        {
            if (ThrowForClientSecret)
                throw new ArgumentException("Client secret credentials require TenantId, ClientId, and ClientSecret.");
            if (credentials is null || string.IsNullOrWhiteSpace(credentials.TenantId) || string.IsNullOrWhiteSpace(credentials.ClientId) || string.IsNullOrWhiteSpace(credentials.ClientSecret))
                throw new ArgumentException("Client secret credentials require TenantId, ClientId, and ClientSecret.");
            return new DummyCredential();
        }
    }

    private sealed class DummyCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new("token", DateTimeOffset.UtcNow.AddMinutes(5));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(new AccessToken("token", DateTimeOffset.UtcNow.AddMinutes(5)));
    }

    private sealed class FakeAppConfigurationClient : IAppConfigurationClient
    {
        public bool ThrowAuth { get; set; }
        public bool ThrowList { get; set; }
        public List<ConfigurationSetting> Settings { get; } = new();
        public ConfigurationSetting? SetResult { get; set; }
        public bool ThrowOnSet { get; set; }

        public AsyncPageable<ConfigurationSetting> GetConfigurationSettingsAsync(string keyFilter = "*", string labelFilter = "*", CancellationToken ct = default)
        {
            if (ThrowAuth) throw new AuthenticationFailedException("auth failed");
            if (ThrowList) throw new RequestFailedException(500, "list failed");

            IEnumerable<ConfigurationSetting> filtered = Settings;
            if (keyFilter != "*")
            {
                var prefix = keyFilter.TrimEnd('*');
                filtered = filtered.Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            }

            if (labelFilter != "*")
            {
                filtered = filtered.Where(s => string.Equals(s.Label, labelFilter, StringComparison.OrdinalIgnoreCase));
            }

            return AsyncPageable<ConfigurationSetting>.FromPages(
                new[] { Page<ConfigurationSetting>.FromValues(filtered.ToList(), null, new TestResponse(200)) });
        }

        public Task<ConfigurationSetting?> GetConfigurationSettingAsync(string key, string? label = null, CancellationToken ct = default)
        {
            var match = Settings.FirstOrDefault(s =>
                string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase) &&
                (label == null || string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase)));
            return Task.FromResult(match);
        }

        public Task<ConfigurationSetting> SetConfigurationSettingAsync(ConfigurationSetting setting, CancellationToken ct = default)
        {
            if (ThrowOnSet) throw new RequestFailedException(409, "locked");
            var result = SetResult ?? setting;
            // Update in-memory list
            var existing = Settings.FindIndex(s =>
                string.Equals(s.Key, setting.Key, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.Label, setting.Label, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                Settings[existing] = result;
            else
                Settings.Add(result);
            return Task.FromResult(result);
        }
    }

    private sealed class FakeAppConfigurationClientFactory : IAppConfigurationClientFactory
    {
        public FakeAppConfigurationClient Instance { get; } = new();
        public IAppConfigurationClient Create(Uri endpoint, TokenCredential credential) => Instance;
    }

    private sealed class TestResponse : Response
    {
        private readonly int _status;
        public TestResponse(int status) => _status = status;
        public override int Status => _status;
        public override string ReasonPhrase => "OK";
        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; } = string.Empty;
        public override void Dispose() { }
        protected override bool ContainsHeader(string name) => false;
        protected override bool TryGetHeader(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value) { value = null; return false; }
        protected override bool TryGetHeaderValues(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IEnumerable<string>? values) { values = null; return false; }
        protected override IEnumerable<HttpHeader> EnumerateHeaders() => Array.Empty<HttpHeader>();
    }

    private sealed class FakeAzureIntegrationManagerService : IAzureIntegrationManagerService
    {
        public SecretProviderCredentials? Credentials { get; set; }

        public Task<AzureIntegrationManager?> GetManagerAsync()
            => Task.FromResult(Credentials is null ? null : new AzureIntegrationManager(Credentials, DateTime.UtcNow));

        public Task<SecretProviderCredentials?> GetClientSecretCredentialsAsync()
            => Task.FromResult(Credentials);

        public Task<Result<AzureIntegrationManager>> UpdateClientSecretCredentialsAsync(SecretProviderCredentials credentials)
            => Task.FromResult(Result<AzureIntegrationManager>.Success(new AzureIntegrationManager(credentials, DateTime.UtcNow)));
    }

    private static SecretProvider NewProvider(SecretProviderAuthMode mode, SecretProviderCredentials? creds = null) => new(
        Id: Guid.NewGuid(),
        Name: "app-config",
        VaultUri: new Uri("https://unit-test.azconfig.io/"),
        AuthMode: mode,
        Credentials: creds,
        Capabilities: SecretProviderCapabilities.Check,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: DateTime.UtcNow);

    [Fact]
    public async Task TestConnection_Success()
    {
        var credentialFactory = new FakeCredentialFactory();
        var appFactory = new FakeAppConfigurationClientFactory();
        appFactory.Instance.Settings.Add(new ConfigurationSetting("alpha", "value"));
        var client = new AzureAppConfigurationClient(credentialFactory, appFactory);

        var result = await client.TestConnectionAsync(new Uri("https://unit-test.azconfig.io/"), SecretProviderAuthMode.ManagedIdentity, null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ListKeyValues_IdentifiesKeyVaultReferences_AndHidesReferenceValue()
    {
        var credentialFactory = new FakeCredentialFactory();
        var appFactory = new FakeAppConfigurationClientFactory();
        appFactory.Instance.Settings.Add(new ConfigurationSetting("Shared:ApiUrl", "https://example"));
        appFactory.Instance.Settings.Add(new ConfigurationSetting("Shared:Secret", "{\"uri\":\"https://vault.vault.azure.net/secrets/a\"}")
        {
            ContentType = "application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8"
        });
        var client = new AzureAppConfigurationClient(credentialFactory, appFactory);

        var result = await client.ListKeyValuesAsync(NewProvider(SecretProviderAuthMode.ManagedIdentity));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        var kvRef = result.Value.Single(i => i.Key == "Shared:Secret");
        Assert.True(kvRef.IsKeyVaultReference);
        Assert.Null(kvRef.Value);
        Assert.Equal("https://vault.vault.azure.net/secrets/a", kvRef.KeyVaultReferenceUri);
    }

    [Fact]
    public async Task ClientSecretProvider_UsesSharedManagerCredentials_WhenProviderCredentialsMissing()
    {
        var credentialFactory = new FakeCredentialFactory();
        var appFactory = new FakeAppConfigurationClientFactory();
        appFactory.Instance.Settings.Add(new ConfigurationSetting("Key", "Value"));
        var manager = new FakeAzureIntegrationManagerService
        {
            Credentials = new SecretProviderCredentials("tenant", "client", "secret")
        };
        var client = new AzureAppConfigurationClient(credentialFactory, appFactory, manager);

        var result = await client.ListKeyValuesAsync(NewProvider(SecretProviderAuthMode.ClientSecret, null));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task SetKeyValue_CreatesNewEntry_WhenKeyDoesNotExist()
    {
        var credentialFactory = new FakeCredentialFactory();
        var appFactory = new FakeAppConfigurationClientFactory();
        var client = new AzureAppConfigurationClient(credentialFactory, appFactory);

        var result = await client.SetKeyValueAsync(NewProvider(SecretProviderAuthMode.ManagedIdentity), "App:NewKey", null, "newvalue", null);

        Assert.True(result.IsSuccess);
        Assert.Equal("App:NewKey", result.Value!.Key);
        Assert.Equal("newvalue", result.Value.Value);
    }

    [Fact]
    public async Task SetKeyValue_UpdatesExistingEntry()
    {
        var credentialFactory = new FakeCredentialFactory();
        var appFactory = new FakeAppConfigurationClientFactory();
        appFactory.Instance.Settings.Add(new ConfigurationSetting("App:Key", "old-value", "prod"));
        var client = new AzureAppConfigurationClient(credentialFactory, appFactory);

        var result = await client.SetKeyValueAsync(NewProvider(SecretProviderAuthMode.ManagedIdentity), "App:Key", "prod", "new-value", null);

        Assert.True(result.IsSuccess);
        Assert.Equal("App:Key", result.Value!.Key);
        Assert.Equal("new-value", result.Value.Value);
    }

    [Fact]
    public async Task GetKeyValue_ReturnsNull_WhenKeyDoesNotExist()
    {
        var credentialFactory = new FakeCredentialFactory();
        var appFactory = new FakeAppConfigurationClientFactory();
        var client = new AzureAppConfigurationClient(credentialFactory, appFactory);

        var result = await client.GetKeyValueAsync(NewProvider(SecretProviderAuthMode.ManagedIdentity), "missing-key");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetKeyValue_ReturnsEntry_WhenKeyExists()
    {
        var credentialFactory = new FakeCredentialFactory();
        var appFactory = new FakeAppConfigurationClientFactory();
        appFactory.Instance.Settings.Add(new ConfigurationSetting("App:Key", "stored-value"));
        var client = new AzureAppConfigurationClient(credentialFactory, appFactory);

        var result = await client.GetKeyValueAsync(NewProvider(SecretProviderAuthMode.ManagedIdentity), "App:Key");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("App:Key", result.Value!.Key);
        Assert.Equal("stored-value", result.Value.Value);
    }
}
