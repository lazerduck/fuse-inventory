using Fuse.Core.Configs;
using Fuse.Core.Models;
using Fuse.Data.Stores;
using Xunit;

namespace Fuse.Tests.Stores;

public class JsonFuseStoreTests : IDisposable
{
    private readonly string _testDataDirectory;

    public JsonFuseStoreTests()
    {
        _testDataDirectory = Path.Combine(Path.GetTempPath(), $"fuse-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDirectory))
        {
            Directory.Delete(_testDataDirectory, true);
        }
    }

    [Fact]
    public async Task LoadAsync_LoadsCurrentSecretBindingFormat()
    {
        var accountId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var accountsJson = $$"""
        [
            {
                "id": "{{accountId}}",
                "targetId": "{{targetId}}",
                "targetKind": "External",
                "authKind": "ApiKey",
                "secretBinding": {
                    "kind": "AzureKeyVault",
                    "plainReference": null,
                    "azureKeyVault": {
                        "providerId": "{{providerId}}",
                        "secretName": "db-password",
                        "version": "v1"
                    }
                },
                "userName": null,
                "parameters": null,
                "grants": [],
                "tagIds": [],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;
        await File.WriteAllTextAsync(Path.Combine(_testDataDirectory, "accounts.json"), accountsJson);

        var externalResourcesJson = $$"""
        [
            {
                "id": "{{targetId}}",
                "name": "Test Resource",
                "description": null,
                "uri": "https://example.com",
                "tagIds": [],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;
        await File.WriteAllTextAsync(Path.Combine(_testDataDirectory, "externalresources.json"), externalResourcesJson);

        var secretProvidersJson = $$"""
        [
            {
                "id": "{{providerId}}",
                "name": "Test Provider",
                "vaultUri": "https://test.vault.azure.net",
                "authMode": "ManagedIdentity",
                "credentials": null,
                "capabilities": 1,
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;
        await File.WriteAllTextAsync(Path.Combine(_testDataDirectory, "secretproviders.json"), secretProvidersJson);

        await CreateMinimalDataFiles();

        var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });

        var snapshot = await store.LoadAsync();

        Assert.Single(snapshot.Accounts);
        var account = snapshot.Accounts[0];
        Assert.Equal(accountId, account.Id);
        Assert.Equal(AuthKind.ApiKey, account.AuthKind);
        Assert.Equal(SecretBindingKind.AzureKeyVault, account.SecretBinding.Kind);
        Assert.NotNull(account.SecretBinding.AzureKeyVault);
        Assert.Equal(providerId, account.SecretBinding.AzureKeyVault!.ProviderId);
        Assert.Equal("db-password", account.SecretBinding.AzureKeyVault.SecretName);
        Assert.Equal("v1", account.SecretBinding.AzureKeyVault.Version);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsCurrentSecretBindingFormat()
    {
        var accountId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        await CreateMinimalDataFiles();

        var store = new JsonFuseStore(new JsonFuseStoreOptions { DataDirectory = _testDataDirectory });

        var externalResource = new ExternalResource(
            Id: targetId,
            Name: "Test Resource",
            Description: null,
            ResourceUri: new Uri("https://example.com"),
            TagIds: new HashSet<Guid>(),
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );

        var secretProvider = new SecretProvider(
            Id: providerId,
            Name: "Test Provider",
            VaultUri: new Uri("https://test.vault.azure.net"),
            AuthMode: SecretProviderAuthMode.ManagedIdentity,
            Credentials: null,
            Capabilities: SecretProviderCapabilities.Check,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );

        var account = new Account(
            Id: accountId,
            TargetId: targetId,
            TargetKind: TargetKind.External,
            AuthKind: AuthKind.ApiKey,
            SecretBinding: new SecretBinding(
                Kind: SecretBindingKind.AzureKeyVault,
                PlainReference: null,
                AzureKeyVault: new AzureKeyVaultBinding(
                    ProviderId: providerId,
                    SecretName: "my-secret",
                    Version: null
                )
            ),
            UserName: null,
            Parameters: null,
            Grants: Array.Empty<Grant>(),
            TagIds: new HashSet<Guid>(),
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );

        var snapshot = new Snapshot(
            Applications: Array.Empty<Application>(),
            DataStores: Array.Empty<DataStore>(),
            Platforms: Array.Empty<Platform>(),
            ExternalResources: new[] { externalResource },
            Accounts: new[] { account },
            Identities: Array.Empty<Identity>(),
            Tags: Array.Empty<Tag>(),
            Environments: Array.Empty<EnvironmentInfo>(),
            KumaIntegrations: Array.Empty<KumaIntegration>(),
            SecretProviders: new[] { secretProvider },
            SqlIntegrations: Array.Empty<SqlIntegration>(),
            Positions: Array.Empty<Position>(),
            ResponsibilityTypes: Array.Empty<ResponsibilityType>(),
            ResponsibilityAssignments: Array.Empty<ResponsibilityAssignment>(),
            Risks: Array.Empty<Risk>(),
            Security: new SecurityState(new SecuritySettings(SecurityLevel.None, DateTime.UtcNow), Array.Empty<SecurityUser>())
        );

        await store.SaveAsync(snapshot);

        var reloaded = await store.LoadAsync();
        var reloadedAccount = Assert.Single(reloaded.Accounts);
        Assert.Equal(SecretBindingKind.AzureKeyVault, reloadedAccount.SecretBinding.Kind);
        Assert.NotNull(reloadedAccount.SecretBinding.AzureKeyVault);
        Assert.Equal(providerId, reloadedAccount.SecretBinding.AzureKeyVault!.ProviderId);
        Assert.Equal("my-secret", reloadedAccount.SecretBinding.AzureKeyVault.SecretName);
    }

    private async Task CreateMinimalDataFiles()
    {
        var emptyArray = "[]";

        var filesToCreate = new[]
        {
            "applications.json",
            "datastores.json",
            "platforms.json",
            "externalresources.json",
            "accounts.json",
            "identities.json",
            "tags.json",
            "environments.json",
            "kumaintegrations.json",
            "secretproviders.json",
            "sqlintegrations.json",
            "positions.json",
            "responsibilitytypes.json",
            "responsibilityassignments.json",
            "risks.json"
        };

        foreach (var file in filesToCreate)
        {
            var path = Path.Combine(_testDataDirectory, file);
            if (!File.Exists(path))
            {
                await File.WriteAllTextAsync(path, emptyArray);
            }
        }

        var securityPath = Path.Combine(_testDataDirectory, "security.json");
        if (!File.Exists(securityPath))
        {
            var securityJson = """
            {
                "settings": {
                    "level": "None",
                    "updatedAt": "2024-01-01T00:00:00Z"
                },
                "users": []
            }
            """;
            await File.WriteAllTextAsync(securityPath, securityJson);
        }
    }
}
