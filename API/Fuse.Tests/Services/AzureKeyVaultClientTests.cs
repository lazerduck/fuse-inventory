using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;
using Fuse.Core.Services;
using Xunit;

namespace Fuse.Tests.Services;

public class AzureKeyVaultClientTests
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
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("token", DateTimeOffset.UtcNow.AddMinutes(5));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => new(new AccessToken("token", DateTimeOffset.UtcNow.AddMinutes(5)));
    }

    private sealed class FakeSecretClient : IKeyVaultSecretClient
    {
        private readonly Dictionary<string, List<(string Version, string Value)>> _secrets = new();
        public bool ThrowAuth { get; set; }
        public bool ThrowList { get; set; }
        public bool ThrowSet { get; set; }
        public bool ThrowGet { get; set; }

        private static KeyVaultSecret MakeSecret(string name, string value) => new(name, value);

        public Task<Response<KeyVaultSecret>> GetSecretAsync(string name, CancellationToken ct = default)
        {
            if (ThrowGet) throw new RequestFailedException(404, "Not found");
            if (!_secrets.TryGetValue(name, out var versions)) throw new RequestFailedException(404, "Not found");
            var data = versions[^1];
            var secret = MakeSecret(name, data.Value);
            return Task.FromResult(Response.FromValue(secret, new TestResponse(200)));
        }

        public Task<Response<KeyVaultSecret>> GetSecretVersionAsync(string name, string version, CancellationToken ct = default)
        {
            if (ThrowGet) throw new RequestFailedException(404, "Not found");
            if (!_secrets.TryGetValue(name, out var versions)) throw new RequestFailedException(404, "Not found");
            var data = versions.FirstOrDefault(v => v.Version == version);
            if (data.Version == null) throw new RequestFailedException(404, "Not found");
            var secret = MakeSecret(name, data.Value);
            return Task.FromResult(Response.FromValue(secret, new TestResponse(200)));
        }

        public Task<Response<KeyVaultSecret>> SetSecretAsync(string name, string value, CancellationToken ct = default)
        {
            if (ThrowSet) throw new RequestFailedException(500, "Failed");
            var version = Guid.NewGuid().ToString("N");
            if (!_secrets.ContainsKey(name)) _secrets[name] = new List<(string Version, string Value)>();
            _secrets[name].Add((version, value));
            var secret = MakeSecret(name, value);
            return Task.FromResult(Response.FromValue(secret, new TestResponse(200)));
        }

        public AsyncPageable<SecretProperties> GetPropertiesOfSecretsAsync(CancellationToken ct = default)
        {
            if (ThrowAuth) throw new AuthenticationFailedException("auth failed");
            if (ThrowList) throw new RequestFailedException(500, "list failed");
            var latestSecrets = _secrets.Select(kv => MakeSecret(kv.Key, kv.Value[^1].Value).Properties).ToList();
            return AsyncPageable<SecretProperties>.FromPages(new[] { Page<SecretProperties>.FromValues(latestSecrets, null, new TestResponse(200)) });
        }

        public string? GetFirstVersion(string name) => _secrets.TryGetValue(name, out var list) ? list.First().Version : null;
        public string? GetLatestVersion(string name) => _secrets.TryGetValue(name, out var list) ? list.Last().Version : null;
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

    private sealed class FakeSecretClientFactory : IKeyVaultSecretClientFactory
    {
        public FakeSecretClient Instance { get; } = new();
        public IKeyVaultSecretClient Create(Uri vaultUri, TokenCredential credential) => Instance;
    }

    private static SecretProvider NewProvider(SecretProviderAuthMode mode, SecretProviderCapabilities caps, SecretProviderCredentials? creds = null) => new(
        Id: Guid.NewGuid(),
        Name: "prov",
        VaultUri: new Uri("https://unit-test.vault.azure.net/"),
        AuthMode: mode,
        Credentials: creds,
        Capabilities: caps,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: DateTime.UtcNow);

    [Fact]
    public async Task TestConnection_Success()
    {
        var credentialFactory = new FakeCredentialFactory();
        var secretFactory = new FakeSecretClientFactory();
        // Add a secret so enumeration returns something
        await secretFactory.Instance.SetSecretAsync("alpha", "value");
        var client = new AzureKeyVaultClient(credentialFactory, secretFactory);
        var result = await client.TestConnectionAsync(new Uri("https://unit-test.vault.azure.net/"), SecretProviderAuthMode.ManagedIdentity, null);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TestConnection_AuthenticationFailure()
    {
        var credentialFactory = new FakeCredentialFactory();
        var secretFactory = new FakeSecretClientFactory();
        secretFactory.Instance.ThrowAuth = true;
        var client = new AzureKeyVaultClient(credentialFactory, secretFactory);
        var result = await client.TestConnectionAsync(new Uri("https://unit-test.vault.azure.net/"), SecretProviderAuthMode.ManagedIdentity, null);
        Assert.False(result.IsSuccess);
        Assert.Contains("Authentication failed", result.Error);
    }

    [Fact]
    public async Task CreateSecret_SetsValue()
    {
        var cf = new FakeCredentialFactory();
        var sf = new FakeSecretClientFactory();
        var client = new AzureKeyVaultClient(cf, sf);
        var provider = NewProvider(SecretProviderAuthMode.ManagedIdentity, SecretProviderCapabilities.Create);
        var result = await client.CreateSecretAsync(provider, "my-secret", "val1");
        Assert.True(result.IsSuccess);
        var read = await sf.Instance.GetSecretAsync("my-secret");
        Assert.Equal("val1", read.Value.Value);
    }

    [Fact]
    public async Task CreateSecret_FailurePropagates()
    {
        var cf = new FakeCredentialFactory();
        var sf = new FakeSecretClientFactory();
        sf.Instance.ThrowSet = true;
        var client = new AzureKeyVaultClient(cf, sf);
        var provider = NewProvider(SecretProviderAuthMode.ManagedIdentity, SecretProviderCapabilities.Create);
        var result = await client.CreateSecretAsync(provider, "s", "v");
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to create secret", result.Error);
    }

    [Fact]
    public async Task RotateSecret_CreatesNewVersion()
    {
        var cf = new FakeCredentialFactory();
        var sf = new FakeSecretClientFactory();
        var client = new AzureKeyVaultClient(cf, sf);
        var provider = NewProvider(SecretProviderAuthMode.ManagedIdentity, SecretProviderCapabilities.Rotate);
        await client.RotateSecretAsync(provider, "s", "v1");
        await client.RotateSecretAsync(provider, "s", "v2");
        var read = await sf.Instance.GetSecretAsync("s");
        Assert.Equal("v2", read.Value.Value);
        var enumerator = sf.Instance.GetPropertiesOfSecretsAsync().GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task ReadSecret_LatestAndVersion()
    {
        var cf = new FakeCredentialFactory();
        var sf = new FakeSecretClientFactory();
        var client = new AzureKeyVaultClient(cf, sf);
        var provider = NewProvider(SecretProviderAuthMode.ManagedIdentity, SecretProviderCapabilities.Read);
        await client.CreateSecretAsync(provider, "a", "one");
        await client.RotateSecretAsync(provider, "a", "two");
        var latest = await client.ReadSecretAsync(provider, "a");
        Assert.True(latest.IsSuccess);
        Assert.Equal("two", latest.Value);
        var firstVersion = sf.Instance.GetFirstVersion("a")!;
        var versionResp = await client.ReadSecretAsync(provider, "a", firstVersion);
        Assert.True(versionResp.IsSuccess);
    }

    [Fact]
    public async Task ReadSecret_NotFound()
    {
        var cf = new FakeCredentialFactory();
        var sf = new FakeSecretClientFactory();
        sf.Instance.ThrowGet = true;
        var client = new AzureKeyVaultClient(cf, sf);
        var provider = NewProvider(SecretProviderAuthMode.ManagedIdentity, SecretProviderCapabilities.Read);
        var res = await client.ReadSecretAsync(provider, "missing");
        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorType.NotFound, res.ErrorType);
    }

    [Fact]
    public async Task ListSecrets_ReturnsMetadata()
    {
        var cf = new FakeCredentialFactory();
        var sf = new FakeSecretClientFactory();
        await sf.Instance.SetSecretAsync("s1", "v1");
        await sf.Instance.SetSecretAsync("s2", "v2");
        var client = new AzureKeyVaultClient(cf, sf);
        var provider = NewProvider(SecretProviderAuthMode.ManagedIdentity, SecretProviderCapabilities.Check);
        var list = await client.ListSecretsAsync(provider);
        Assert.True(list.IsSuccess);
        Assert.Equal(2, list.Value!.Count);
    }

    [Fact]
    public async Task CredentialFactory_ClientSecretValidationFailure()
    {
        var cf = new FakeCredentialFactory { ThrowForClientSecret = true };
        var sf = new FakeSecretClientFactory();
        var client = new AzureKeyVaultClient(cf, sf);
        var result = await client.TestConnectionAsync(new Uri("https://unit-test.vault.azure.net"), SecretProviderAuthMode.ClientSecret, new SecretProviderCredentials(null, null, null));
        Assert.False(result.IsSuccess);
        Assert.Contains("Client secret credentials", result.Error);
    }

    [Fact]
    public async Task UnsupportedAuthMode_ProducesFailure()
    {
        var cf = new FakeCredentialFactory();
        var sf = new FakeSecretClientFactory();
        var client = new AzureKeyVaultClient(cf, sf);
        var res = await client.TestConnectionAsync(new Uri("https://u"), (SecretProviderAuthMode)999, null);
        Assert.False(res.IsSuccess);
        Assert.Contains("Unsupported auth mode", res.Error);
    }
}
